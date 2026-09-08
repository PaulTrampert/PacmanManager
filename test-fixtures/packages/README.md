# Package fixtures

`pacmanmanager-test-1.2.3-4-x86_64.pkg.tar.zst` is a real, minimal pacman package. It is loaded by
`LibAlpmSharp.Test` through `ILibAlpm.LoadPackageFile` and is the package the RepoHost publishing
end to end tests upload, so its metadata is asserted on in more than one place — change it only
deliberately, and update the tests that pin its values.

`pacmanmanager-test-1.2.4-1-x86_64.pkg.tar.zst` is the same package one `pkgver` on, and differs in
nothing else. Publishing refuses an upload that does not move the version forward, so replacing a
published package takes a second, newer file rather than a second push of the first one.

The packages are committed rather than built during the test run so that the tests need nothing but
libalpm. `build-fixture.sh` regenerates both byte for byte and is the record of what is in them.

| Field | Value | Upgrade |
| :--- | :--- | :--- |
| `pkgname` | `pacmanmanager-test` | `pacmanmanager-test` |
| `pkgver` | `1.2.3-4` | `1.2.4-1` |
| `arch` | `x86_64` | `x86_64` |

Locate them from a test with `PackageFixtures.MinimalPackagePath` and
`PackageFixtures.UpgradePackagePath` in `PacmanManager.TestUtils` rather than by composing the path
again.

The same metadata is what `PacmanManager.TestUtils.LocalPackageDatabase` writes into the local
database it seeds under a temporary root, so a test reading an *installed* package sees the fields
above rather than whatever the host has installed. Change a value here and the constants in
`PackageFixtures` carry it to both.
