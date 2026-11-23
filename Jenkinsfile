// CI/CD: test -> build 4 images -> push to Azure Container Registry -> DB migrations -> deploy to k3s.
// Jenkins credentials required (Manage Jenkins > Credentials):
//   acr-credentials  : Username/Password  (ACR admin user or a service principal with AcrPush)
//   k3s-kubeconfig   : Secret file         (k3s kubeconfig, server address reachable from Jenkins)
//   azure-pg         : Username/Password  (Azure Database for PostgreSQL admin login)
// Agent requirements: docker + kubectl on PATH, and the Postgres firewall must allow the agent's IP
// (see deploy/AZURE_DEPLOY.md).
pipeline {
  agent any

  parameters {
    string(name: 'ACR_NAME', defaultValue: 'fundooacr', description: 'Azure Container Registry name (without .azurecr.io)')
    string(name: 'PG_HOST', defaultValue: 'fundoo-pg.postgres.database.azure.com', description: 'Azure PostgreSQL server host name')
    booleanParam(name: 'RUN_MIGRATIONS', defaultValue: true, description: 'Run EF Core + SQL migrations before deploying')
  }

  environment {
    REGISTRY  = "${params.ACR_NAME}.azurecr.io"
    IMAGE_TAG = "${env.BUILD_NUMBER}-${env.GIT_COMMIT ? env.GIT_COMMIT.take(7) : 'local'}"
  }

  options {
    timestamps()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '20'))
  }

  stages {
    stage('Unit tests') {
      steps {
        // Runs in the .NET 8 SDK container so the agent needs no dotnet install.
        sh '''
          # Source is mounted read-only and copied in, so no root-owned bin/obj land in the workspace.
          mkdir -p TestResults
          docker run --rm -v "$WORKSPACE":/repo:ro -v "$WORKSPACE/TestResults":/results \
            mcr.microsoft.com/dotnet/sdk:8.0 bash -c '
              set -e
              mkdir /work && cp -r /repo/src /repo/tests /work/ && cd /work
              dotnet test tests/Fundoo.UnitTests/Fundoo.UnitTests.csproj -c Release \
                --logger "trx;LogFileName=results.trx" --results-directory /results
              chmod -R a+rw /results'
        '''
      }
      post { always { archiveArtifacts artifacts: 'TestResults/*.trx', allowEmptyArchive: true } }
    }

    stage('Build images') {
      steps {
        script {
          services().each { name, proj ->
            sh """
              docker build \
                --build-arg PROJECT=${proj}.csproj \
                --build-arg DLL=\$(basename ${proj}).dll \
                -t ${REGISTRY}/fundoo-${name}:${IMAGE_TAG} \
                -t ${REGISTRY}/fundoo-${name}:latest .
            """
          }
        }
      }
    }

    stage('Push to ACR') {
      steps {
        withCredentials([usernamePassword(credentialsId: 'acr-credentials', usernameVariable: 'ACR_USER', passwordVariable: 'ACR_PASS')]) {
          sh 'echo "$ACR_PASS" | docker login "$REGISTRY" -u "$ACR_USER" --password-stdin'
          script {
            services().each { name, proj ->
              sh "docker push ${REGISTRY}/fundoo-${name}:${IMAGE_TAG} && docker push ${REGISTRY}/fundoo-${name}:latest"
            }
          }
        }
      }
    }

    // Runs before the deploy so new code never starts against an old schema.
    // Every migration is additive/idempotent, so pods still on the previous version keep working.
    stage('DB migrations') {
      when { expression { params.RUN_MIGRATIONS } }
      steps {
        withCredentials([usernamePassword(credentialsId: 'azure-pg', usernameVariable: 'PG_USER', passwordVariable: 'PG_PASSWORD')]) {
          sh 'chmod +x deploy/db/migrate.sh && PG_HOST="$PG_HOST" deploy/db/migrate.sh'
        }
      }
    }

    stage('Deploy to k3s') {
      steps {
        withCredentials([
          file(credentialsId: 'k3s-kubeconfig', variable: 'KUBECONFIG'),
          usernamePassword(credentialsId: 'acr-credentials', usernameVariable: 'ACR_USER', passwordVariable: 'ACR_PASS')
        ]) {
          sh '''
            set -e
            # Point the kustomization at this build's images.
            sed -i -e "s#fundooacr.azurecr.io#${REGISTRY}#g" -e "s#newTag: latest#newTag: ${IMAGE_TAG}#g" deploy/kustomization.yaml

            kubectl apply -f deploy/k8s/00-namespace-config.yaml
            # Registry pull secret (re-created each run so rotated passwords are picked up).
            kubectl -n fundoo create secret docker-registry acr-pull \
              --docker-server="$REGISTRY" --docker-username="$ACR_USER" --docker-password="$ACR_PASS" \
              --dry-run=client -o yaml | kubectl apply -f -

            kubectl -n fundoo get secret fundoo-secrets >/dev/null || { echo "Secret fundoo-secrets missing - see deploy/AZURE_DEPLOY.md step 6"; exit 1; }

            # Jobs are immutable: delete so they re-run with the new spec.
            kubectl -n fundoo delete job kafka-topics --ignore-not-found
            kubectl apply -k deploy/

            for d in user-api notes-api notification-api api-gateway; do
              kubectl -n fundoo rollout status deployment/$d --timeout=300s
            done
            kubectl -n fundoo get pods -o wide
          '''
        }
      }
    }
  }

  post {
    always { sh 'docker logout "$REGISTRY" || true' }
    failure {
      withCredentials([file(credentialsId: 'k3s-kubeconfig', variable: 'KUBECONFIG')]) {
        sh 'kubectl -n fundoo get pods; kubectl -n fundoo get events --sort-by=.lastTimestamp | tail -30 || true'
      }
    }
  }
}

def services() {
  return [
    'user-api'        : 'src/Services/User/Fundoo.User.Api/Fundoo.User.Api',
    'notes-api'       : 'src/Services/Notes/Fundoo.Notes.Api/Fundoo.Notes.Api',
    'notification-api': 'src/Services/Notification/Fundoo.Notification.Api/Fundoo.Notification.Api',
    'api-gateway'     : 'src/ApiGateway/Fundoo.ApiGateway/Fundoo.ApiGateway',
  ]
}
