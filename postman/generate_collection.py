"""Generates the Postman collection + environments for FundoMicroservices.

Run:  python3 postman/generate_collection.py
Outputs postman/FundooMicroservices.postman_collection.json and *.postman_environment.json
"""
import json
import pathlib
import uuid

OUT = pathlib.Path(__file__).parent

JSON_HEADER = [{"key": "Content-Type", "value": "application/json"}]


def req(name, method, base_var, path, body=None, auth=False, tests=None, description=""):
    segments = [s for s in path.strip("/").split("/") if s]
    item = {
        "name": name,
        "request": {
            "method": method,
            "header": list(JSON_HEADER) if body is not None else [],
            "url": {
                "raw": "{{%s}}/%s" % (base_var, "/".join(segments)),
                "host": ["{{%s}}" % base_var],
                "path": segments,
            },
            "description": description,
        },
        "response": [],
    }
    if body is not None:
        item["request"]["body"] = {
            "mode": "raw",
            "raw": json.dumps(body, indent=2),
            "options": {"raw": {"language": "json"}},
        }
    if auth:
        item["request"]["auth"] = {
            "type": "bearer",
            "bearer": [{"key": "token", "value": "{{accessToken}}", "type": "string"}],
        }
    else:
        item["request"]["auth"] = {"type": "noauth"}
    if tests:
        item["event"] = [{"listen": "test", "script": {"type": "text/javascript", "exec": tests}}]
    return item


def status_test(*codes):
    codes_js = ", ".join(str(c) for c in codes)
    return [f"pm.test('status is one of [{codes_js}]', () => pm.expect(pm.response.code).to.be.oneOf([{codes_js}]));"]


def folder(name, items, description=""):
    return {"name": name, "description": description, "item": items}


# ---------------- User service (api/auth) ----------------
user = folder(
    "User Service - Auth",
    [
        req("Register", "POST", "baseUrl", "api/auth/register",
            {"firstName": "Test", "lastName": "User", "email": "{{email}}", "password": "{{password}}"},
            tests=status_test(200, 201, 400, 409),
            description="Creates an account. Sends a welcome email through the Notification service."),
        req("Login", "POST", "baseUrl", "api/auth/login",
            {"email": "{{email}}", "password": "{{password}}"},
            tests=status_test(200) + [
                "const j = pm.response.json();",
                "if (j.userId) pm.collectionVariables.set('userId', j.userId);",
                "if (j.accessToken) pm.collectionVariables.set('accessToken', j.accessToken);",
                "if (j.requiresTwoFactor) console.log('2FA required: copy the OTP from the email into the otpCode variable, then run Verify 2FA');",
            ],
            description="Login always triggers an emailed OTP (2FA). Put the code into `otpCode` and run **Verify 2FA**."),
        req("Verify 2FA (OTP)", "POST", "baseUrl", "api/auth/2fa/verify",
            {"email": "{{email}}", "code": "{{otpCode}}"},
            tests=status_test(200) + [
                "const j = pm.response.json();",
                "pm.test('access token issued', () => pm.expect(j.accessToken).to.be.a('string'));",
                "pm.collectionVariables.set('accessToken', j.accessToken);",
                "if (j.userId) pm.collectionVariables.set('userId', j.userId);",
            ],
            description="Exchanges the emailed OTP for a JWT; saves it to `accessToken`."),
        req("Forgot Password", "POST", "baseUrl", "api/auth/forgot-password",
            {"email": "{{email}}"}, tests=status_test(200)),
        req("Reset Password", "POST", "baseUrl", "api/auth/reset-password",
            {"email": "{{email}}", "token": "{{resetToken}}", "newPassword": "{{newPassword}}"},
            tests=status_test(200, 400),
            description="`resetToken` comes from the password-reset email."),
        req("Google SSO - start (open in browser)", "GET", "baseUrl", "api/auth/google",
            description="Redirects to Google. Must be opened in a browser, not Postman."),
        req("Google SSO - callback", "GET", "baseUrl", "api/auth/google/callback",
            description="Called by Google after sign-in. Listed for completeness only."),
    ],
    "Fundoo.User.Api — `api/auth/*`. Anonymous endpoints.",
)

# ---------------- Notes service (api/notes) ----------------
note_body = {"title": "My first note", "content": "Hello from Postman", "color": "#FFF475", "isPinned": False}
notes = folder(
    "Notes Service - Notes",
    [
        req("Create Note", "POST", "baseUrl", "api/notes", note_body, auth=True,
            tests=status_test(201) + [
                "const j = pm.response.json();",
                "pm.collectionVariables.set('noteId', j.data.id);",
            ]),
        req("Get Active Notes", "GET", "baseUrl", "api/notes", auth=True, tests=status_test(200)),
        req("Get Note By Id", "GET", "baseUrl", "api/notes/{{noteId}}", auth=True, tests=status_test(200)),
        req("Update Note", "PUT", "baseUrl", "api/notes/{{noteId}}",
            {**note_body, "title": "Updated title", "isPinned": True}, auth=True, tests=status_test(200)),
        req("Archive Note", "PUT", "baseUrl", "api/notes/{{noteId}}/archive", auth=True, tests=status_test(200, 204)),
        req("Get Archived Notes", "GET", "baseUrl", "api/notes/archived", auth=True, tests=status_test(200)),
        req("Unarchive Note", "PUT", "baseUrl", "api/notes/{{noteId}}/unarchive", auth=True, tests=status_test(200, 204)),
        req("Trash Note", "PUT", "baseUrl", "api/notes/{{noteId}}/trash", auth=True, tests=status_test(200, 204)),
        req("Get Trashed Notes", "GET", "baseUrl", "api/notes/trash", auth=True, tests=status_test(200)),
        req("Restore Note", "PUT", "baseUrl", "api/notes/{{noteId}}/restore", auth=True, tests=status_test(200, 204)),
    ],
    "Fundoo.Notes.Api — `api/notes/*`. Requires Bearer JWT from Verify 2FA.",
)

labels = folder(
    "Notes Service - Labels",
    [
        req("Create Label", "POST", "baseUrl", "api/labels", {"name": "Work"}, auth=True,
            tests=status_test(201) + [
                "const j = pm.response.json();",
                "pm.collectionVariables.set('labelId', j.data.id);",
            ]),
        req("Get Labels", "GET", "baseUrl", "api/labels", auth=True, tests=status_test(200)),
        req("Update Label", "PUT", "baseUrl", "api/labels/{{labelId}}", {"name": "Work-Renamed"}, auth=True,
            tests=status_test(200)),
        req("Attach Label to Note", "PUT", "baseUrl", "api/labels/{{noteId}}/labels/{{labelId}}", auth=True,
            tests=status_test(200, 204)),
        req("Detach Label from Note", "DELETE", "baseUrl", "api/labels/{{noteId}}/labels/{{labelId}}", auth=True,
            tests=status_test(200, 204)),
        req("Delete Label", "DELETE", "baseUrl", "api/labels/{{labelId}}", auth=True, tests=status_test(200, 204)),
        req("Delete Note (permanent)", "DELETE", "baseUrl", "api/notes/{{noteId}}", auth=True,
            tests=status_test(200, 204)),
    ],
    "Fundoo.Notes.Api — `api/labels/*`. Note: attach/detach routes live under /api/labels/{noteId}/labels/{labelId}.",
)

# ---------------- Notification service (internal) ----------------
notif = folder(
    "Notification Service (internal)",
    [
        req("Send Welcome Email", "POST", "notificationUrl", "api/notifications/welcome",
            {"recipientEmail": "{{email}}", "recipientName": "Test User"}, tests=status_test(202)),
        req("Send Password Reset Email", "POST", "notificationUrl", "api/notifications/password-reset",
            {"recipientEmail": "{{email}}", "recipientName": "Test User", "resetToken": "sample-token"},
            tests=status_test(202)),
        req("Send 2FA OTP Email", "POST", "notificationUrl", "api/notifications/2fa-otp",
            {"recipientEmail": "{{email}}", "recipientName": "Test User", "otpCode": "123456", "expiryMinutes": 10},
            tests=status_test(202)),
    ],
    "Fundoo.Notification.Api — not exposed through the gateway. Locally: http://localhost:5230; on k3s: `kubectl port-forward svc/notification-api 5230:80 -n fundoo`.",
)

health = folder(
    "Health",
    [
        req("User API live", "GET", "userUrl", "health/live", tests=status_test(200)),
        req("Notes API live", "GET", "notesUrl", "health/live", tests=status_test(200)),
        req("Notes API ready (Postgres)", "GET", "notesUrl", "health/ready", tests=status_test(200)),
    ],
)

collection = {
    "info": {
        "_postman_id": str(uuid.uuid5(uuid.NAMESPACE_URL, "fundoo-microservices")),
        "name": "Fundoo Microservices",
        "description": (
            "All APIs found in FundoMicroservices.\n\n"
            "Flow: Register -> Login (OTP emailed) -> set `otpCode` -> Verify 2FA (saves `accessToken`) -> Notes/Labels.\n"
            "`baseUrl` points at the Ocelot API Gateway (routes /api/auth, /api/notes, /api/labels)."
        ),
        "schema": "https://schema.getpostman.com/json/collection/v2.1.0/collection.json",
    },
    "item": [user, notes, labels, notif, health],
    "variable": [
        {"key": k, "value": ""} for k in ["accessToken", "userId", "noteId", "labelId"]
    ],
}

(OUT / "FundooMicroservices.postman_collection.json").write_text(json.dumps(collection, indent=2) + "\n")


def env(name, values):
    return {
        "id": str(uuid.uuid5(uuid.NAMESPACE_URL, "fundoo-env-" + name)),
        "name": name,
        "values": [
            {"key": k, "value": v, "type": "secret" if "password" in k.lower() else "default", "enabled": True}
            for k, v in values.items()
        ],
        "_postman_variable_scope": "environment",
    }


common = {"email": "testuser@example.com", "password": "Test@12345", "newPassword": "Test@54321",
          "otpCode": "", "resetToken": ""}
envs = {
    "Fundoo - Local (dotnet run)": {
        "baseUrl": "https://localhost:7000", "userUrl": "https://localhost:7001",
        "notesUrl": "https://localhost:7002", "notificationUrl": "http://localhost:5230", **common},
    "Fundoo - Azure k3s": {
        "baseUrl": "http://<VM_PUBLIC_IP_OR_DNS>", "userUrl": "http://localhost:5195",
        "notesUrl": "http://localhost:5077", "notificationUrl": "http://localhost:5230", **common},
}
for name, values in envs.items():
    fname = name.split(" - ")[1].split(" ")[0].lower()
    (OUT / f"Fundoo-{fname}.postman_environment.json").write_text(json.dumps(env(name, values), indent=2) + "\n")

print("done")
