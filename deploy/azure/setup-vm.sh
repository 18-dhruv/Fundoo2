#!/usr/bin/env bash
# One-shot bootstrap for the Fundoo VM (Ubuntu 22.04/24.04): k3s + Docker + Jenkins, fully configured.
# Run on the VM as a sudo-capable user:
#
#   sudo PG_HOST=<server>.postgres.database.azure.com PG_USER=<admin> \
#        REPO_URL=https://github.com/<you>/<repo>.git bash setup-vm.sh
#
# Optional: ACR_NAME/ACR_USER/ACR_PASS to push images to Azure Container Registry instead of loading
# them straight into k3s, BRANCH (default main).
# Passwords are NOT passed here: run deploy/azure/set-secrets.sh afterwards (it prompts for them).
#
# Jenkins is configured as code (JCasC): admin user (random password in /root/jenkins-admin-password),
# the credentials the Jenkinsfile needs (k3s-kubeconfig, azure-pg, acr-credentials) and a
# "fundoo" pipeline job that builds the branch.
set -euo pipefail
: "${PG_HOST:?}" "${PG_USER:?}" "${REPO_URL:?}"
BRANCH="${BRANCH:-main}"
ACR_NAME="${ACR_NAME:-}"; ACR_USER="${ACR_USER:-unused}"; ACR_PASS="${ACR_PASS:-unused}"
export DEBIAN_FRONTEND=noninteractive

echo "==> Swap (Docker builds + Kafka + Jenkins on 8 GB)"
if ! swapon --show | grep -q /swapfile; then
  fallocate -l 4G /swapfile && chmod 600 /swapfile && mkswap /swapfile >/dev/null && swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "==> Docker, Java"
apt-get update -qq
apt-get install -y -qq docker.io fontconfig openjdk-21-jre-headless curl gnupg >/dev/null
systemctl enable --now docker
# Docker sets the iptables FORWARD policy to DROP when it starts; after a reboot that can cut off k3s pod traffic.
install -d /etc/systemd/system/docker.service.d
printf '[Service]\nExecStartPost=/usr/sbin/iptables -P FORWARD ACCEPT\n' > /etc/systemd/system/docker.service.d/k3s-forward.conf
systemctl daemon-reload && iptables -P FORWARD ACCEPT

echo "==> k3s"
if ! command -v k3s >/dev/null; then
  curl -sfL https://get.k3s.io | INSTALL_K3S_EXEC="--write-kubeconfig-mode 644" sh -
fi
until kubectl get nodes 2>/dev/null | grep -q " Ready"; do sleep 3; done

echo "==> Jenkins"
if ! dpkg -s jenkins >/dev/null 2>&1; then
  curl -fsSL https://pkg.jenkins.io/debian-stable/jenkins.io-2026.key -o /usr/share/keyrings/jenkins-keyring.asc
  echo "deb [signed-by=/usr/share/keyrings/jenkins-keyring.asc] https://pkg.jenkins.io/debian-stable binary/" > /etc/apt/sources.list.d/jenkins.list
  apt-get update -qq && apt-get install -y -qq jenkins >/dev/null
fi
usermod -aG docker jenkins
# Lets the pipeline load freshly built images into k3s without a registry (and nothing else).
echo 'jenkins ALL=(root) NOPASSWD: /usr/local/bin/k3s ctr images import -' > /etc/sudoers.d/jenkins-k3s-import
chmod 440 /etc/sudoers.d/jenkins-k3s-import && visudo -cf /etc/sudoers.d/jenkins-k3s-import >/dev/null
systemctl stop jenkins

JH=/var/lib/jenkins
echo "==> Jenkins plugins"
PM=/opt/jenkins-plugin-manager.jar
[ -f $PM ] || curl -fsSL -o $PM https://github.com/jenkinsci/plugin-installation-manager-tool/releases/download/2.13.2/jenkins-plugin-manager-2.13.2.jar
java -jar $PM --war /usr/share/java/jenkins.war --plugin-download-directory $JH/plugins \
  --plugins workflow-aggregator git credentials-binding configuration-as-code job-dsl timestamper pipeline-stage-view >/dev/null
chown -R jenkins:jenkins $JH/plugins

echo "==> Jenkins configuration as code"
install -d -o jenkins -g jenkins $JH/casc
cat > $JH/casc/jenkins.yaml <<EOF
jenkins:
  systemMessage: "Fundoo CI/CD"
  numExecutors: 1
  globalNodeProperties:
    - envVars:
        env:
          - key: FUNDOO_PG_HOST
            value: "${PG_HOST}"
  securityRealm:
    local:
      allowsSignup: false
      users:
        - id: admin
          password: "\${JENKINS_ADMIN_PASS}"
  authorizationStrategy:
    loggedInUsersCanDoAnything:
      allowAnonymousRead: false
credentials:
  system:
    domainCredentials:
      - credentials:
          - usernamePassword:
              id: acr-credentials
              scope: GLOBAL
              username: "\${ACR_USER}"
              password: "\${ACR_PASS}"
          - usernamePassword:
              id: azure-pg
              scope: GLOBAL
              username: "\${PG_USER}"
              password: "\${PG_PASS}"
          - file:
              id: k3s-kubeconfig
              scope: GLOBAL
              fileName: k3s.yaml
              secretBytes: "\${readFileBase64:/etc/rancher/k3s/k3s.yaml}"
unclassified:
  location:
    url: "\${JENKINS_URL}"
jobs:
  - script: >
      pipelineJob('fundoo') {
        parameters {
          stringParam('ACR_NAME', '${ACR_NAME}', 'Azure Container Registry name')
          stringParam('PG_HOST', '', 'Azure PostgreSQL host (empty = FUNDOO_PG_HOST)')
          booleanParam('RUN_MIGRATIONS', true, 'Run DB migrations before deploy')
        }
        triggers { scm('H/5 * * * *') }
        definition {
          cpsScm {
            scm { git { remote { url('${REPO_URL}') }; branch('*/${BRANCH}') } }
            scriptPath('Jenkinsfile')
            lightweight(true)
          }
        }
      }
EOF
chown jenkins:jenkins $JH/casc/jenkins.yaml && chmod 600 $JH/casc/jenkins.yaml

# Secrets reach JCasC as environment variables of the Jenkins service only (file is root/jenkins 600).
PUBLIC_IP=$(curl -s -H Metadata:true "http://169.254.169.254/metadata/instance/network/interface/0/ipv4/ipAddress/0/publicIpAddress?api-version=2021-02-01&format=text" || true)
install -d /etc/systemd/system/jenkins.service.d
cat > /etc/systemd/system/jenkins.service.d/override.conf <<EOF
[Service]
Environment="JAVA_OPTS=-Djava.awt.headless=true -Djenkins.install.runSetupWizard=false -Xmx1g"
Environment="CASC_JENKINS_CONFIG=$JH/casc/jenkins.yaml"
EnvironmentFile=/etc/jenkins-casc.env
EOF
umask 077
[ -s /root/jenkins-admin-password ] || openssl rand -base64 18 | tr -d '/+=' > /root/jenkins-admin-password
JENKINS_ADMIN_PASS=$(cat /root/jenkins-admin-password)
# Keep a Postgres password set earlier by set-secrets.sh when this script is re-run.
PG_PASS=$(sed -n 's/^PG_PASS=//p' /etc/jenkins-casc.env 2>/dev/null || true); PG_PASS="${PG_PASS:-not-set-yet}"
cat > /etc/jenkins-casc.env <<EOF
JENKINS_ADMIN_PASS=$JENKINS_ADMIN_PASS
ACR_USER=$ACR_USER
ACR_PASS=$ACR_PASS
PG_USER=$PG_USER
PG_PASS=$PG_PASS
JENKINS_URL=http://${PUBLIC_IP:-localhost}:8080/
EOF
chown root:root /etc/jenkins-casc.env
systemctl daemon-reload
systemctl enable --now jenkins

echo "==> Waiting for Jenkins"
until curl -s -o /dev/null -w '%{http_code}' http://localhost:8080/login | grep -q 200; do sleep 5; done
echo "==> Done. Jenkins: http://${PUBLIC_IP:-<vm-ip>}:8080  (user: admin, password: sudo cat /root/jenkins-admin-password)"
echo "==> Next: sudo bash set-secrets.sh   (enter the Postgres and SMTP passwords)"
