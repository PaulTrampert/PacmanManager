// The Keycloak admin REST calls the end-to-end tests need, made as the bootstrap admin that
// compose.yaml creates. See docs/web-ui-architecture/web-ui-architecture.md, "End-to-end tests".
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

/** The Keycloak client the web host signs users in as, and refreshes their tokens with. */
export const webHostClientId = "pacman-manager-web";

/**
 * Ends every Keycloak session `username` has, so the web host can no longer refresh their tokens:
 * signs their sessions out, then revokes the offline token the web host holds for them, which a
 * sign-out leaves working (the web host asks for `offline_access`). Their web host cookie survives
 * until it next needs Keycloak.
 */
export async function signOutSessions(username: string): Promise<void> {
  const id = await userId(username);
  await adminRequest(`/users/${id}/logout`, `sign ${username}'s sessions out`, {
    method: "POST",
  });
  // Revoking the client's consent revokes its offline tokens too. Keycloak answers 404 when the user
  // has neither, which leaves nothing to revoke.
  const token = await adminToken();
  const response = await fetch(
    `${keycloakUrl}/admin/realms/${realm}/users/${id}/consents/${webHostClientId}`,
    { method: "DELETE", headers: { Authorization: `Bearer ${token}` } },
  );
  if (response.status !== 404) {
    await ensureOk(response, `revoke ${username}'s offline tokens`);
  }
}

/** The client attribute that overrides the realm's access token lifespan, in seconds. */
const accessTokenLifespanAttribute = "access.token.lifespan";

/** The part of Keycloak's client representation these helpers read; the rest is sent back as is. */
interface ClientRepresentation {
  id: string;
  attributes?: Record<string, string | null>;
}

async function clientRepresentation(
  clientId: string,
): Promise<ClientRepresentation> {
  const response = await adminRequest(
    `/clients?${new URLSearchParams({ clientId })}`,
    `find the client ${clientId}`,
  );
  const clients = (await response.json()) as ClientRepresentation[];
  if (clients.length !== 1) {
    throw new KeycloakAdminError(
      `Expected one client ${clientId} in the ${realm} realm, found ${clients.length}.`,
    );
  }
  return clients[0];
}

/**
 * Sets how long the access tokens Keycloak issues to the client `clientId` live, in seconds,
 * overriding the realm's lifespan; `undefined` removes the override, which keycloak/localdev.json
 * does not have. Tokens already issued keep their lifespan.
 */
export async function setAccessTokenLifespan(
  clientId: string,
  seconds: number | undefined,
): Promise<void> {
  const client = await clientRepresentation(clientId);
  await adminRequest(
    `/clients/${client.id}`,
    `set the access token lifespan of ${clientId}`,
    {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        ...client,
        attributes: {
          ...client.attributes,
          // Keycloak removes an attribute sent as null.
          [accessTokenLifespanAttribute]:
            seconds === undefined ? null : String(seconds),
        },
      }),
    },
  );
}
