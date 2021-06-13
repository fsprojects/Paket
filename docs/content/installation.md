# Paket installation

This guide will show you

* How to set up Paket for a specific repository.
* How to install Paket for Windows, Linux, or macOS.
* How to ensure `paket.exe` is available via command line and other methods of
  use.
* Install [editor support](editor-support.html).
* Set up [shell completion](shell-completion.html) for Paket commands.

## Installation on .NET Core

### Global tool

Paket can be used a global tool

```sh
dotnet tool install --global Paket
```

### Local tool

With the .NET Core 3 CLI, you can now use Local Tools.

```sh
dotnet new tool-manifest

dotnet tool install Paket
```

Don't forget to commit `dotnet-tools.json` to your source control.

Now you can run Paket with:

```sh
dotnet paket --help
```


## Installation per repository

The most common use of Paket is as a command line tool inside your project
repository.

1. Create a `.paket` directory in the root of your solution.
1. Download the latest
   [`paket.bootstrapper.exe`](https://github.com/fsprojects/Paket/releases/latest)
   into that directory.
1. Run `.paket/paket.bootstrapper.exe`. It will download the latest `paket.exe`.
   > linux/osx: Run `mono .paket/paket.bootstrapper.exe`
1. Commit `.paket/paket.bootstrapper.exe` into your repository and add
   `.paket/paket.exe` to your `.gitignore` file.

You can now run Paket from the command line:

```sh
.paket/paket.exe install
```

## System-wide Installation

If you want to install Paket as a system-wide tool then the following guide will
help you to get started.

### Cloning the Paket repository

The first step for any operating system is to clone the [Paket
repository](https://github.com/fsprojects/Paket) locally.

```sh
git clone git@github.com:fsprojects/Paket.git
```

Then follow the instructions for building and installing for your preferred
operating system.

### Installation on Linux

For Linux the easiest way to get things installed is to clone the repository and
run the build and install scripts as shown below.

```sh
./build.sh
```

After that completes execute the install, to install Paket as a command line
utility.

```sh
./install.sh
```

The `install.sh` script will add Paket as a command line option into bash and
most other shells available. If you are using a unique shell and run into
problems, please post an issue so we can take a look.

### Installation on macOS

For macOS the build and installation process is as follows.

```sh
./build.sh
```

After that completes execute the install, to install Paket as a command line
utility.

```sh
./install.sh
```

### Installation on Windows

Please [install per repository](installation.html#Installation-per-repository).

### Post installation

Once the basic installation is complete on your operating system of choice it is
often very useful to add some tools to your shell/IDE/Text Editor of choice.

* [Editor support](editor-support.html)
* [Shell completion](shell-completion.html)

For next steps check out the [Getting Started](get-started.html) section.
