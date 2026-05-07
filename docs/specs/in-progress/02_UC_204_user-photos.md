{
  "$schema": ".claude/schemas/spec.v1.json",
  "uc_id": "UC-204",
  "slug": "user-photos",
  "title": "User profile photo upload + delete via Azure Blob SAS",
  "actors": [
    "Authenticated mobile app user (registered Wander profile)"
  ],
  "preconditions": [
    "User has registered (User row exists for the JWT sub claim).",
    "Azure Blob Storage is reachable; locally via Azurite emulator at the connection string in configuration.",
    "Azurite container is running (docker-compose service 'azurite' on :10000).",
    "Phase 2.1/2.2 entities and migration are applied — UserPhoto table has unique index on (user_id, order).",
    "Storage account holds a single container named 'user-photos' (create on startup if missing). Blob path convention: {userId}/photos/{photoId}.jpg."
  ],
  "main_flow": [
    "Mobile app calls POST /api/v1/users/me/photos with body { order: int (0-3) }. Order is the display slot.",
    "Endpoint resolves the caller's User by JWT sub claim. 401 if sub missing; 404 + User.NotRegistered if no User row.",
    "Endpoint validates the order is 0-3 (validator); validates the user has fewer than ValidationConstants.MaxPhotosPerUser non-deleted photos (endpoint guard, count via AsNoTracking().CountAsync); validates the order slot is not already occupied by a non-deleted photo (endpoint guard).",
    "Endpoint generates a new photo id and constructs the blob path '{userId}/photos/{photoId}.jpg'.",
    "Endpoint creates a UserPhoto row with BlobUrl set to the CDN/blob path (the location the upload will land at) and saves.",
    "Endpoint generates a write-only Azure Blob SAS URL using the user-scoped container path. SAS permissions = Create | Write only (no read, no list, no delete). SAS expires in 10 minutes.",
    "Endpoint returns 201 with { photoId, blobUrl, sasUrl, sasExpiresAt }.",
    "Mobile app uploads the JPEG bytes directly to the SAS URL via PUT.",
    "Mobile app later calls DELETE /api/v1/users/me/photos/{photoId} when removing a photo.",
    "Endpoint resolves the photo by id, scoped to the caller (UserPhoto.UserId == caller). 404 if not found or not owned.",
    "Endpoint sets DeletedAt = now on the UserPhoto row (soft delete). Best-effort: deletes the blob from storage; if storage delete fails, log but still succeed (storage cleanup can be retried by a background job in a later phase).",
    "Endpoint returns 204 No Content."
  ],
  "alternate_flows": [
    "User already has 4 non-deleted photos → POST returns 400 with code Validation.PhotoLimitReached.",
    "Order slot already taken by a non-deleted photo → POST returns 400 with code Validation.PhotoOrderTaken.",
    "Order outside 0-3 range → POST returns 400 with code Validation.PhotoOrderOutOfRange.",
    "Soft-deleted photos do NOT count toward the 4-photo limit (CitiesCount-style soft-delete semantics).",
    "Storage account misconfigured at startup (missing connection string) → both POST and DELETE return 503 with code Storage.NotConfigured.",
    "Blob delete fails on DELETE — log warning, continue, return 204. The UserPhoto row is still soft-deleted; orphan blobs cleaned up later.",
    "DELETE on already-deleted photo → 404 (treat soft-deleted as not found from the API perspective)."
  ],
  "acceptance_criteria": [
    "POST /api/v1/users/me/photos returns 201 with a body containing photoId (Guid), blobUrl (string), sasUrl (string), sasExpiresAt (DateTimeOffset).",
    "POST persists exactly one UserPhoto row with the requested Order, UserId = caller, BlobUrl matching the {userId}/photos/{photoId}.jpg pattern, CreatedAt = TimeProvider.GetUtcNow(), DeletedAt = null.",
    "POST returns 400 with code Validation.PhotoOrderOutOfRange when Order is < 0 or > 3 (validator).",
    "POST returns 400 with code Validation.PhotoLimitReached when the caller has 4 non-deleted photos.",
    "POST returns 400 with code Validation.PhotoOrderTaken when a non-deleted photo already occupies that order slot.",
    "POST returns 401 when no Bearer token is presented.",
    "POST returns 404 with code User.NotRegistered when the JWT sub maps to no User row.",
    "POST applies the GeneralApi rate limit policy.",
    "Returned SAS URL grants Create+Write only, expires within 10 minutes of generation, and is scoped to the specific blob path (not the container) — a write attempt to a different blob in the same container with the same SAS must fail.",
    "DELETE /api/v1/users/me/photos/{id} returns 204 on success and sets DeletedAt = now on the UserPhoto row.",
    "DELETE returns 404 when the photo id is unknown or owned by a different user.",
    "DELETE returns 404 when the photo is already soft-deleted.",
    "DELETE returns 401 when no Bearer token is presented.",
    "Both endpoints have FastEndpoints Validator<TRequest> classes for input shape (POST validates Order; DELETE validates Id is non-empty Guid via route constraint).",
    "Endpoints are internal sealed, inherit Endpoint<TRequest, TResponse> (or no-response variant for DELETE), declare DontCatchExceptions(), use Send.* pattern, sit under Features/Users/UploadPhoto/ and Features/Users/DeletePhoto/.",
    "An IBlobStorageService abstraction lives in WanderMeet.Infrastructure and is registered via UsersFeatureConfiguration.AddFeatureDependencies (feature owns its dep). The Azure SDK is the production implementation; tests use the same implementation pointed at Azurite via the test factory's ConfigureTestServices override.",
    "Integration tests cover: POST happy path (201, body shape, DB row, SAS write to the blob succeeds, SAS write to a different path fails); POST 4-photo limit; DELETE happy path (DeletedAt set, blob removed); DELETE not-owned (404). At least one test asserts the SAS URL actually allows a write through the Azure SDK (BlobClient with SAS URL).",
    "Unit tests cover: validator order range; endpoint 503 when storage misconfigured (use Microsoft.Extensions.Options.Options.Create with empty options)."
  ],
  "out_of_scope": [
    "Image content validation (size, dimensions, MIME) — Azure Blob just stores bytes; client is expected to upload JPEG.",
    "CDN URL generation — Phase 1.7 work; for now BlobUrl is the direct blob storage URL.",
    "Background job to clean up orphan blobs (blobs without a UserPhoto row) — Phase 3+ Hangfire-replacement BackgroundService work.",
    "Reordering photos (PATCH on order) — separate slice if needed later.",
    "Reading photos via signed URLs — public CDN handles read; out of this UC.",
    "Moderation (flagging inappropriate photos) — separate slice tied to the Reports feature.",
    "Production Azure Storage account provisioning — handled in Phase 1.1 by the operator."
  ],
  "non_functional": [
    "P95 < 400 ms for POST (one DB roundtrip + one SAS generation + one SaveChanges).",
    "P95 < 300 ms for DELETE (one DB roundtrip + one SaveChanges + one async blob delete).",
    "SAS URLs MUST NOT be logged. Storage connection string MUST NOT be logged.",
    "Blob path scoping is critical: a SAS issued for user A must not allow writing to user B's prefix. Verified by an integration test attempting cross-user write.",
    "Azurite container starts with the docker-compose stack; local dev does not require a real Azure Storage account.",
    "All async calls forward CancellationToken; tests use TestContext.Current.CancellationToken."
  ]
}
