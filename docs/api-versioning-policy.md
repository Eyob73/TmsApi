API Versioning Policy

Purpose
This one-page policy defines what the TMS considers breaking vs. non-breaking API changes, the sunset window for older versions, communication requirements, and version-skipping rules. It is intended to give engineering teams a clear, repeatable decision process so migrations are predictable and auditable.

1. Breaking changes
   The following kinds of changes are considered breaking and require a new major API version (for example, v1 → v2):

- Removing an existing public field/property from a response or request contract.
- Renaming or changing the meaning of an existing field.
- Changing an endpoint's primary URL path (e.g., `/api/v1/courses` → `/api/v1/classes`).
- Changing HTTP status codes for existing success or error flows (e.g., switching 200 → 204 or 400 → 404 for the same semantic case).
- Tightening validation rules that can cause previously-accepted requests to be rejected (e.g., shorter allowed string length, stricter regex, required previously-optional field).
- Changing default sorting, pagination semantic, or response shape in a way that alters client-visible ordering or required parsing logic.

2. Additive (non-breaking) changes
   The following are considered non-breaking and may be released on the same major version:

- Adding new optional fields to request or response payloads (clients that ignore unknown fields continue to work).
- Adding new endpoints or resources that do not change existing endpoints' behaviour.
- Adding optional query parameters or headers that have sensible defaults and do not change existing behaviour when absent.
- Extending error payloads with additional metadata fields.

3. Sunset window

- TMS guarantees a minimum 6-month sunset window after a newer major version is released. This accommodates clients with quarterly maintenance cycles and remote deployments.
- The sunset window starts on the public release date of the successor major version (e.g., v2 release date). After the window ends, v1 will be deprecated and removed from production following the communicated schedule.

4. Communication requirements
   When releasing a new major version, the releasing team must:

- Add `Deprecation`, `Sunset`, and `Link` headers to responses from the old version starting on the day the new version is released. The `Link` header should point to the successor version endpoint and use `rel="successor-version"`.
- Create a CHANGELOG entry that explains the breaking changes, migration guidance, and timeline.
- Send an email to every team or customer that holds an API key with a summary, migration steps, and the sunset date.
- Create a calendar invite for the v1 shutdown date and include migration resources and contact info.

5. Skipping versions

- Clients may migrate directly from vN to vM (e.g., v1 → v3) without passing through intermediate versions. The team releasing vM must provide clear migration guidance showing delta changes since vN.

End of policy
