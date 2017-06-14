# Paket Installation Options

This guide will show you

  * How to set up Paket for a specific repository.
  * How to install Paket for Windows, Linux, or macOS.
  * How to ensure `paket.exe` is available via command line and other methods of use.

## Installation per repository

The most common use of Paket is as a command line tool inside your project repository.

  * Create a `.paket` folder in the root of your solution.
  * Download the latest [paket.bootstrapper.exe](https://github.com/fsprojects/Paket/releases/latest) into that folder.
  * Run `.paket/paket.bootstrapper.exe`. This will download the latest `paket.exe`.
  * Commit `.paket/paket.bootstrapper.exe` into your repo and add `.paket/paket.exe` to your `.gitignore` file.

You can now run Paket from the command line:

    $ .paket/paket.exe install

## System-wide Installation

If you want to install Paket as a system-wide tool then the following guide will help you to get started.

### Cloning the Paket repository

The first step for any operating system is to clone the [Paket repository](https://github.com/fsprojects/Paket) locally.

    git clone git@github.com:fsprojects/Paket.git
    
Then follow the instructions for building and installing for your preferred operating system.

### Installation on Linux

For Linux the easiest way to get things installed is to clone the repository and run the build and install scripts as shown below.

    ./build.sh

After that completes execute the install, to install Paket as a command line utility.

    ./install.sh
    
The install.sh script will add Paket as a command line option into bash and most other shells available. If you are using a unique shell and run into problems, please post an issue so we can take a look.

### Installation on macOS

For macOS the build and installation process is as follows.

    ./build.sh

After that completes execute the install, to install Paket as a command line utility.

    ./install.sh

### Installation on Windows

Please use the [Installation per repository](installation.html#Installation-per-repository) option.

### Post Installation

Once the basic installation is complete on your operating system of choice it is often very useful to add some tools to your IDE/Text Editor of choice. Here's some of the options that are available.

For next steps check out the [Getting Started](getting-started.html) section.
