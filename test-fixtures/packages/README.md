# Package fixtures

`pacmanmanager-test-1.2.3-4-x86_64.pkg.tar.zst` is a real, minimal pacman package. It is loaded by
`LibAlpmSharp.Test` through `ILibAlpm.LoadPackageFile` and is the package the RepoHost publishing
end to end tests upload, so its metadata is asserted on in more than one place — change it only
deliberately, and update the tests that pin its values.

The package is committed rather than built during the test run so that the tests need nothing but
libalpm. `build-fixture.sh` regenerates it byte for byte and is the record of what is in it.

| Field | Value |
| :--- | :--- |
| `pkgname` | `pacmanmanager-test` |
| `pkgver` | `1.2.3-4` |
| `arch` | `x86_64` |

Locate it from a test with `PackageFixtures.MinimalPackagePath` in `PacmanManager.TestUtils` rather
than by composing the path again.
