import { fetchAntiforgeryToken } from "../api/antiforgery";
import { antiforgeryField, logoutPath } from "./paths";

/**
 * Signs the user out: fetches a fresh antiforgery token, then submits a form `POST` to `/logout`
 * carrying it. The web host ends its cookie session and the Keycloak session, and returns the
 * browser to `/`, so this leaves the app.
 *
 * @throws {AntiforgeryError} when the web host issues no antiforgery token.
 */
export async function signOut(): Promise<void> {
  const token = await fetchAntiforgeryToken();
  const form = document.createElement("form");
  form.method = "post";
  form.action = logoutPath;
  form.hidden = true;
  const field = document.createElement("input");
  field.type = "hidden";
  field.name = antiforgeryField;
  field.value = token;
  form.append(field);
  document.body.append(form);
  form.submit();
}
