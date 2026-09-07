# Packages API

## Public Model
The public API model should reflect the information available via libalpm. Additionally the model should have an `id` property, which is a GUIDv7, used as the primary key in the database, and an `ownerId`, which is the id of the user that published the package. Properties that correspond to information available from libalpm should be validated according to libalpm's rules. The `name` of a package should be unique within a repository.

## Routes

### GET /api/v1/packages
Gets a paginated list of packages that are visible to the current user. May be called anonymously. Visibility is controlled at the repository level. If a caller may see a repository, they may see all packages within the repository.

#### Query Parameters (all are optional)
* `repositoryIds` - Filters packages by repositoryId. More than one may be provided, separated by commas.
* `ownerIds` - Filters packages by their publisher. More than one may be provided, separated by commas.
* `updatedSince` - DateTime parameter, ISO8601. Returns all packages with a published datetime later than the provided DateTime.
* `search` - Text search. Performs a full-text search over the packages.

Additionally supports pagination and sorting parameters using the existing `PaginationParams` and `SortOptions` models.

#### Aliases
* `GET /api/v1/repositories/{repositoryId}/packages`
  * Only difference here is that this route does not accept the `repositoryIds` query parameter. Instead the `repositoryId` is provided in the path.

### POST /api/v1/repositories/{repositoryId}/packages
Takes a pacman package file in the request body. Upon completion of the file upload, use LibAlpmSharp to extract the package information. If we already have a database record for this package (identified by repositoryId + package name), we should update the existing record with the new information. Otherwise, we create a new record. Either way, we then need to update the actual pacman repository with repo-add.

Access to this route should be restricted to the repository owner for new packages, and the package owner for existing packages. The Action for this policy should be "Publish". This is to allow for changes later where a repository owner may grant other users publish permission.

### DELETE /api/v1/packages/{packageId}
Deletes the specified package. This may only be called by the package owner.

#### Aliases
* `DELETE /api/v1/repositories/{repositoryId}/packages/{name}`

