// The Keycloak admin REST calls the end-to-end tests need, made as the bootstrap admin that
// compose.yaml creates. See docs/web-ui-architecture.md, "End-to-end tests".
import { keycloakUrl, realm } from "./stack.ts";

const admin = { username: "admin", password: "admin" };

/** Thrown when a Keycloak admin call does not succeed. */
export class KeycloakAdminError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "KeycloakAdminError";
  }
}

async function ensureOk(response: Response, action: string): Promise<Response> {
  if (!response.ok) {
    throw new KeycloakAdminError(
      `Keycloak could not ${action}: ${response.status} ${await response.text()}`,
    );
  }
  return response;
}

/** An access token for the admin REST API, issued to the bootstrap admin by the `master` realm. */
export async function adminToken(): Promise<string> {
  const response = await fetch(
    `${keycloakUrl}/realms/master/protocol/openid-connect/token`,
    {
      method: "POST",
      body: new URLSearchParams({
        grant_type: "password",
        client_id: "admin-cli",
        username: admin.username,
        password: admin.password,
      }),
    },
  );
  await ensureOk(response, "issue an admin token");
  const { access_token } = (await response.json()) as { access_token: string };
  return access_token;
}

async function adminRequest(
  path: string,
  action: string,
  init: RequestInit = {},
): Promise<Response> {
  const token = await adminToken();
  const response = await fetch(`${keycloakUrl}/admin/realms/${realm}${path}`, {
    ...init,
    headers: { ...init.headers, Authorization: `Bearer ${token}` },
  });
  return ensureOk(response, action);
}

/** The Keycloak id of the realm user named exactly `username`. */
export async function userId(username: string): Promise<string> {
  const response = await adminRequest(
    `/users?${new URLSearchParams({ username, exact: "true" })}`,
    `find the user ${username}`,
  );
  const users = (await response.json()) as { id: string }[];
  if (users.length !== 1) {
    throw new KeycloakAdminError(
      `Expected one user named ${username} in the ${realm} realm, found ${users.length}.`,
    );
  }
  return users[0].id;
}

/**
 * Ends every Keycloak session `username` has, so the web host can no longer refresh their tokens.
 * Their web host cookie survives until it next needs Keycloak.
 */
export async function signOutSessions(username: string): Promise<void> {
  const id = await userId(username);
  await adminRequest(`/users/${id}/logout`, `sign ${username}'s sessions out`, {
    method: "POST",
  });
}
