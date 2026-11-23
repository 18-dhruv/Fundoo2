# Deploying Fundoo to Azure (k3s + Jenkins + Azure PostgreSQL)

```
            Postman ──HTTP:80──▶ Azure VM (Ubuntu)
                                 ├─ k3s ─ Traefik ingress ─▶ api-gateway (Ocelot)
                                 │                            ├─▶ user-api ──▶ redis, notification-api
                                 │                            └─▶ notes-api ──▶ kafka ──▶ notification-api ──▶ SMTP
                                 └─ Jenkins (:8080) ── builds images ─▶ Azure Container Registry
            user-api / notes-api ──SSL:5432──▶ Azure Database for PostgreSQL Flexible Server
                                                (databases: fundoo_users, fundoo_notes)
```

Run steps 1–3 from your Mac (needs the Azure CLI: `brew install azure-cli`). Change the names in step 0 if they are taken — ACR and Postgres names must be globally unique.

## 0. Variables

```bash
RG=fundoo-rg
LOC=centralindia
ACR=fundooacr$RANDOM          # must be globally unique, lowercase letters/numbers
PG=fundoo-pg-$RANDOM
PG_ADMIN=fundooadmin
PG_PASS='<choose-a-strong-password>'
VM=fundoo-vm
MY_IP=$(curl -s https://ifconfig.me)
```

## 1. Resource group + Container Registry

```bash
az login
az group create -n $RG -l $LOC
az acr create -g $RG -n $ACR --sku Basic --admin-enabled true
az acr credential show -n $ACR      # username + password -> Jenkins credential "acr-credentials"
```

## 2. Azure Database for PostgreSQL (Flexible Server)

```bash
az postgres flexible-server create -g $RG -n $PG -l $LOC \
  --admin-user $PG_ADMIN --admin-password "$PG_PASS" \
  --tier Burstable --sku-name Standard_B1ms --storage-size 32 --version 16 \
  --public-access None
az postgres flexible-server db create -g $RG -s $PG -d fundoo_users
az postgres flexible-server db create -g $RG -s $PG -d fundoo_notes
```

The firewall rule for the VM is added in step 3, once the VM has an IP.

## 3. VM for k3s + Jenkins

Kafka + Jenkins + 4 .NET services need ~6 GB RAM, so use at least `Standard_B2ms` (2 vCPU / 8 GB).

```bash
az vm create -g $RG -n $VM --image Ubuntu2204 --size Standard_B2ms \
  --admin-username azureuser --generate-ssh-keys --public-ip-sku Standard
VM_IP=$(az vm show -d -g $RG -n $VM --query publicIps -o tsv)

az vm open-port -g $RG -n $VM --port 80 --priority 900                       # API (Postman)
az network nsg rule create -g $RG --nsg-name ${VM}NSG -n jenkins-from-me --priority 910 \
  --source-address-prefixes $MY_IP --destination-port-ranges 8080 --access Allow --protocol Tcp   # Jenkins UI, only from you

# Let the VM reach Postgres
az postgres flexible-server firewall-rule create -g $RG -n $PG -r allow-vm \
  --start-ip-address $VM_IP --end-ip-address $VM_IP
echo "ssh azureuser@$VM_IP"
```

## 4. Install k3s, Docker and Jenkins on the VM

```bash
ssh azureuser@$VM_IP
```

On the VM:

```bash
# k3s (includes kubectl + Traefik ingress + local-path storage)
curl -sfL https://get.k3s.io | sh -s - --write-kubeconfig-mode 644
sudo kubectl get nodes

# Docker (Jenkins uses it to build images)
sudo apt-get update && sudo apt-get install -y docker.io fontconfig openjdk-17-jre
# Jenkins LTS
sudo wget -O /usr/share/keyrings/jenkins-keyring.asc https://pkg.jenkins.io/debian-stable/jenkins.io-2023.key
echo "deb [signed-by=/usr/share/keyrings/jenkins-keyring.asc] https://pkg.jenkins.io/debian-stable binary/" | sudo tee /etc/apt/sources.list.d/jenkins.list
sudo apt-get update && sudo apt-get install -y jenkins
sudo usermod -aG docker jenkins && sudo systemctl restart jenkins
sudo cat /var/lib/jenkins/secrets/initialAdminPassword
```

Open `http://<VM_IP>:8080`, paste the password, install the suggested plugins, create your admin user.

## 5. Jenkins credentials

*Manage Jenkins → Credentials → System → Global → Add credentials*

| ID | Kind | Value |
|---|---|---|
| `acr-credentials` | Username with password | from `az acr credential show -n $ACR` |
| `k3s-kubeconfig` | Secret file | upload `/etc/rancher/k3s/k3s.yaml` from the VM (`scp azureuser@$VM_IP:/etc/rancher/k3s/k3s.yaml .`). Jenkins runs on the same VM, so `server: https://127.0.0.1:6443` works as-is. |
| `azure-pg` | Username with password | Postgres admin login (`fundooadmin` / your `$PG_PASS`). Used by the **DB migrations** stage. |

## 6. Create the app secret in the cluster (once)

On the VM (values never go into Git):

```bash
PG_HOST=<pg-name>.postgres.database.azure.com
sudo kubectl create namespace fundoo --dry-run=client -o yaml | sudo kubectl apply -f -
sudo kubectl -n fundoo create secret generic fundoo-secrets \
  --from-literal=ConnectionStrings__UserDb="Host=$PG_HOST;Port=5432;Database=fundoo_users;Username=fundooadmin;Password=<pg-pass>;Ssl Mode=Require" \
  --from-literal=ConnectionStrings__NotesDb="Host=$PG_HOST;Port=5432;Database=fundoo_notes;Username=fundooadmin;Password=<pg-pass>;Ssl Mode=Require" \
  --from-literal=Jwt__SigningKey="$(openssl rand -base64 48)" \
  --from-literal=InternalService__SharedSecret="$(openssl rand -base64 48)" \
  --from-literal=EmailSettings__Host=smtp.gmail.com --from-literal=EmailSettings__Port=587 \
  --from-literal=EmailSettings__SenderEmail='<you@gmail.com>' --from-literal=EmailSettings__SenderName=Fundoo \
  --from-literal=EmailSettings__Username='<you@gmail.com>' --from-literal=EmailSettings__Password='<gmail-app-password>' \
  --from-literal=Authentication__Google__ClientId= --from-literal=Authentication__Google__ClientSecret=
```

SMTP matters: login always sends an OTP email, and you need that code to get a token.

## 7. Pipeline job

1. The code is on `main` in your GitHub repo.
2. Jenkins → *New Item* → **Pipeline** → *Pipeline script from SCM* → Git → your repo URL, branch `*/main`, script path `Jenkinsfile`.
3. *Build with Parameters* → `ACR_NAME` = your `$ACR` value, `PG_HOST` = `<pg-name>.postgres.database.azure.com` → Build.

The pipeline runs the unit tests, builds the 4 images, pushes them to ACR, runs the database migrations, then creates the `acr-pull` secret and the Kafka topics and rolls out the services.

**Migrations run from Jenkins** (stage *DB migrations*, script [`deploy/db/migrate.sh`](db/migrate.sh)), before the new pods start:
- User DB: `dotnet ef database update` with the EF Core migrations in `Fundoo.User.Infrastructure`, so a new migration added to the code is applied on the next build.
- Notes DB: `deploy/db/notes-db.sql` (idempotent).

Both run in throwaway Docker containers on the Jenkins agent, which connects straight to Azure Postgres (the firewall rule from step 3 allows the VM). Untick `RUN_MIGRATIONS` to skip them. To run them by hand from the VM: `PG_HOST=... PG_USER=fundooadmin PG_PASSWORD=... deploy/db/migrate.sh`.

Check it on the VM:

```bash
sudo kubectl -n fundoo get pods
sudo kubectl -n fundoo logs job/kafka-topics
curl -i http://localhost/api/auth/login -H 'Content-Type: application/json' -d '{"email":"x@y.com","password":"bad"}'   # expect 401
```

## 8. Postman

1. Import `postman/FundooMicroservices.postman_collection.json` and `postman/Fundoo-azure.postman_environment.json`.
2. In the **Fundoo - Azure k3s** environment, set `baseUrl` = `http://<VM_IP>` and `email` = an inbox you can read.
3. Run: **Register → Login → (copy OTP from email into `otpCode`) → Verify 2FA → Notes / Labels** folders.
4. The Notification folder and the per-service health checks call services directly. Port-forward them from the VM first:
   `sudo kubectl -n fundoo port-forward svc/notification-api 5230:80` (and `svc/user-api 5195:80`, `svc/notes-api 5077:80`), or over SSH with `ssh -L 5230:localhost:5230 azureuser@$VM_IP`.

## Teardown (stops all billing)

```bash
az group delete -n $RG --yes --no-wait
```
