# Local Forwarder release process

## Background
This document describes the sequence of publishing a release of Local Forwarder.

### Library
* Verify that *ApplicationInsights-LocalForwarder/src/Library.NuGet/Microsoft.LocalForwarder.Library.nuspec* contains complete and correct list of package references in the *<dependencies/>* section. Remember that this list will *not* automatically update to match NuGet packages referenced by the project file.
* Bump the NuGet version up by modifying the *<version/>* element in the *<metadata/>* section.
* Commit the changes.
* Build the library (AI_LocalForwarder_Library_Signed_Release).
* Upload the built NuGet package onto a NuGet feed. Ensure the feed is mentioned in *ApplicationInsights-LocalForwarder/src/NuGet.config*. The build pushes the package to MyGet on its own, so feel free to use that if able.

### Hosts
* Open *ApplicationInsights-LocalForwarder/src/Hosts.sln*
* Update all projects (use solution-level NuGet package manager) to update to the latest (just published) version of *Microsoft.LocalForwarder.Library* NuGet.
* Commit the changes.
* Build the hosts (AI_LocalForwarder_Host_Signed_Release).
* Create the release tag on GitHub.

