# Glossary

### paket.dependencies

The file is used to specify rules regarding your application's dependencies.

### paket.lock

The file records the concrete dependency resolution of all direct and indirect dependencies of your project.

### paket.references

The file used to specify which dependencies are to be added to installed into the MSBuild projects in your repository.

### paket.template

These files are used to specify rules to create nupkgs by using the paket pack command.

### .paket folder

This folder is used the same way a .nuget folder is used for the NuGet package restore. Place this folder into the root of your solution. It should include the paket.targets and paket.bootstrapper.exe files which can be downloaded from GitHub. The bootstrapper.exe will always download the latest version of the paket.exe file into this folder.