# Paket Installation Options

This guide will show you

  * How to install Paket for Windows, Linux, or OS-X.
  * How to insure `paket` is available via commmand line and other methods of use.

<blockquote>The following guide provides the various installation methods for each of the three top developer operating systems.</blockquote>

The first step for any operatinng system is to clone the [Paket repository]() locally.

    git clone git@github.com:fsprojects/Paket.git
    
Then follow the instructions for building and installing for your preferred operating system.

### Installation on Linux

For Linux the easiest way to get things installed is to clone the repository and run the build and install scripts as shown below.

    ./build.sh

After that completes execute the install, to install Paket as a command line utility.

    ./install.sh
    
The install.sh script will add Paket as a command line option into bash and most other shells available. If you are using a unique shell and run into problems, please post an issue so we can take a look.

### Installation on OS-X

For OS-X the build and installation procecss is as follows.

    ./build.sh

After that completes execute the install, to install Paket as a command line utility.

    ./install.sh

### Installation on Windows

When using Windows, the previous installation methods work, just exchange the `*.sh` files for the `*.cmd` files.

    build.cmd

Then run the install script to install Paket for command line use.   
 
    install.cmd
    
NOTE: If you use git-bash on Windows you can actually follow the Linux installation instructions and just use the `*.sh` files.

### Post Installation

Once the basic installation is complete on your operating system of choice it is often very useful to add some tools to your IDE/Text Editor of choice. Here's some of the options that are available.

For next steps check out the [Getting Started](getting-started.html) section.