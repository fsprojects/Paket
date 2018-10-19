module Paket.RuntimeGraphTests

open Paket
open NUnit.Framework
open FsUnit
open Paket.Domain
open Paket.TestHelpers

let supportAndDeps = """
{
  "runtimes": {
    "win": {
      "Microsoft.Win32.Primitives": {
        "runtime.win.Microsoft.Win32.Primitives": "4.3.0"
      },
      "System.Runtime.Extensions": {
        "runtime.win.System.Runtime.Extensions": "4.3.0"
      }
    }
  },
  "supports": {
    "uwp.10.0.app": {
      "uap10.0": [
        "win10-x86",
        "win10-x86-aot",
        "win10-arm",
        "win10-arm-aot"
      ]
    },
    "net45.app": {
      "net45": [
        "",
        "win-x86",
        "win-x64"
      ]
    }
  }
}"""


let rids = """
{
    "runtimes": {
        "base": {
        },
        "any": {
            "#import": [ "base" ]
        },
        "win": {
            "#import": [ "any" ]
        },
        "win-x86": {
            "#import": [ "win" ]
        }
   }
}
"""

let completeRids = """
{
    "runtimes": {
        "base": {
        },

        "any": {
            "#import": [ "base" ]
        },

        "android": {
            "#import": [ "any" ]
        },
        "android-arm": {
            "#import": [ "any" ]
        },
        "android-arm64": {
            "#import": [ "any" ]
        },

        "android.21": {
            "#import": [ "android" ]
        },
        "android.21-arm": {
            "#import": [ "android.21", "android-arm" ]
        },
        "android.21-arm64": {
            "#import": [ "android.21", "android-arm64" ]
        },

        "win": {
            "#import": [ "any" ]
        },
        "win-x86": {
            "#import": [ "win" ]
        },
        "win-x64": {
            "#import": [ "win" ]
        },
        "win-arm": {
            "#import": [ "win" ]
        },
        "win-arm64": {
            "#import": [ "win" ]
        },

        "win7": {
            "#import": [ "win" ]
        },
        "win7-x86": {
            "#import": [ "win7", "win-x86" ]
        },
        "win7-x64": {
            "#import": [ "win7", "win-x64" ]
        },

        "win8": {
            "#import": [ "win7" ]
        },
        "win8-x86": {
            "#import": [ "win8", "win7-x86" ]
        },
        "win8-x64": {
            "#import": [ "win8", "win7-x64" ]
        },
        "win8-arm": {
            "#import": [ "win8", "win-arm" ]
        },

        "win81": {
            "#import": [ "win8" ]
        },
        "win81-x86": {
            "#import": [ "win81", "win8-x86" ]
        },
        "win81-x64": {
            "#import": [ "win81", "win8-x64" ]
        },
        "win81-arm": {
            "#import": [ "win81", "win8-arm" ]
        },

        "win10": {
            "#import": [ "win81" ]
        },
        "win10-x86": {
            "#import": [ "win10", "win81-x86" ]
        },
        "win10-x64": {
            "#import": [ "win10", "win81-x64" ]
        },
        "win10-arm": {
            "#import": [ "win10", "win81-arm" ]
        },
        "win10-arm64": {
            "#import": [ "win10", "win-arm64" ]
        },

        "aot": {
            "#import": [ "any" ]
        },

        "win-aot": {
            "#import": [ "win", "aot" ]
        },
        "win-x86-aot": {
            "#import": [ "win-aot", "win-x86" ]
        },
        "win-x64-aot": {
            "#import": [ "win-aot", "win-x64" ]
        },

        "win7-aot": {
            "#import": [ "win-aot", "win7" ]
        },
        "win7-x86-aot": {
            "#import": [ "win7-aot", "win7-x86" ]
        },
        "win7-x64-aot": {
            "#import": [ "win7-aot", "win7-x64" ]
        },

        "win8-aot": {
            "#import": [ "win8", "win7-aot" ]
        },
        "win8-x86-aot": {
            "#import": [ "win8-aot", "win8-x86", "win7-x86-aot" ]
        },
        "win8-x64-aot": {
            "#import": [ "win8-aot", "win8-x64", "win7-x64-aot" ]
        },
        "win8-arm-aot": {
            "#import": [ "win8-aot", "win8-arm" ]
        },

        "win81-aot": {
            "#import": [ "win81", "win8-aot" ]
        },
        "win81-x86-aot": {
            "#import": [ "win81-aot", "win81-x86", "win8-x86-aot" ]
        },
        "win81-x64-aot": {
            "#import": [ "win81-aot", "win81-x64", "win8-x64-aot" ]
        },
        "win81-arm-aot": {
            "#import": [ "win81-aot", "win81-arm", "win8-arm-aot" ]
        },

        "win10-aot": {
            "#import": [ "win10", "win81-aot" ]
        },
        "win10-x86-aot": {
            "#import": [ "win10-aot", "win10-x86", "win81-x86-aot" ]
        },
        "win10-x64-aot": {
            "#import": [ "win10-aot", "win10-x64", "win81-x64-aot" ]
        },
        "win10-arm-aot": {
            "#import": [ "win10-aot", "win10-arm", "win81-arm-aot" ]
        },
        "win10-arm64-aot": {
            "#import": [ "win10-aot", "win10-arm64" ]
        },

        "unix": {
            "#import": [ "any" ]
        },
        "unix-x64": {
            "#import": [ "unix" ]
        },
        "unix-x86": {
            "#import": [ "unix" ]
        },
        "unix-arm": {
            "#import": [ "unix" ]
        },
        "unix-armel": {
            "#import": [ "unix" ]
        },
        "unix-arm64": {
            "#import": [ "unix" ]
        },

        "osx": {
            "#import": [ "unix" ]
        },
        "osx-x64": {
            "#import": [ "osx", "unix-x64" ]
        },

        "osx.10.10": {
            "#import": [ "osx" ]
        },
        "osx.10.10-x64": {
            "#import": [ "osx.10.10", "osx-x64" ]
        },

        "osx.10.11": {
            "#import": [ "osx.10.10" ]
        },
        "osx.10.11-x64": {
            "#import": [ "osx.10.11", "osx.10.10-x64" ]
        },

        "osx.10.12": {
            "#import": [ "osx.10.11" ]
        },
        "osx.10.12-x64": {
            "#import": [ "osx.10.12", "osx.10.11-x64" ]
        },

        "linux": {
            "#import": [ "unix" ]
        },
        "linux-x64": {
            "#import": [ "linux", "unix-x64" ]
        },
        "linux-x86": {
            "#import": [ "linux", "unix-x86" ]
        },
        "linux-arm": {
            "#import": [ "linux", "unix-arm" ]
        },
        "linux-armel": {
            "#import": [ "linux", "unix-armel" ]
        },
        "linux-arm64": {
            "#import": [ "linux", "unix-arm64" ]
        },

        "rhel": {
            "#import": [ "linux" ]
        },
        "rhel-x64": {
            "#import": [ "rhel", "linux-x64" ]
        },

        "rhel.7": {
            "#import": [ "rhel" ]
        },
        "rhel.7-x64": {
            "#import": [ "rhel.7", "rhel-x64" ]
        },

        "rhel.7.0": {
            "#import": [ "rhel.7" ]
        },
        "rhel.7.0-x64": {
            "#import": [ "rhel.7.0", "rhel.7-x64" ]
        },

        "rhel.7.1": {
            "#import": [ "rhel.7.0" ]
        },
        "rhel.7.1-x64": {
            "#import": [ "rhel.7.1", "rhel.7.0-x64" ]
        },

        "rhel.7.2": {
            "#import": [ "rhel.7.1" ]
        },
        "rhel.7.2-x64": {
            "#import": [ "rhel.7.2", "rhel.7.1-x64" ]
        },

        "rhel.7.3": {
            "#import": [ "rhel.7.2" ]
        },
        "rhel.7.3-x64": {
            "#import": [ "rhel.7.3", "rhel.7.2-x64" ]
        },

        "rhel.7.4": {
            "#import": [ "rhel.7.3" ]
        },
        "rhel.7.4-x64": {
            "#import": [ "rhel.7.4", "rhel.7.3-x64" ]
        },

        "ol": {
            "#import": [ "rhel" ]
        },
        "ol-x64": {
            "#import": [ "ol", "rhel-x64" ]
        },

        "ol.7": {
            "#import": [ "ol", "rhel.7" ]
        },
        "ol.7-x64": {
            "#import": [ "ol.7", "ol-x64", "rhel.7-x64" ]
        },

        "ol.7.0": {
            "#import": [ "ol.7", "rhel.7.0" ]
        },
        "ol.7.0-x64": {
            "#import": [ "ol.7.0", "ol.7-x64", "rhel.7.0-x64" ]
        },

        "ol.7.1": {
            "#import": [ "ol.7.0", "rhel.7.1" ]
        },
        "ol.7.1-x64": {
            "#import": [ "ol.7.1", "ol.7.0-x64", "rhel.7.1-x64" ]
        },

        "ol.7.2": {
            "#import": [ "ol.7.1", "rhel.7.2" ]
        },
        "ol.7.2-x64": {
            "#import": [ "ol.7.2", "ol.7.1-x64", "rhel.7.2-x64" ]
        },

        "centos": {
            "#import": [ "rhel" ]
        },
        "centos-x64": {
            "#import": [ "centos", "rhel-x64" ]
        },

        "centos.7": {
            "#import": [ "centos", "rhel.7" ]
        },
        "centos.7-x64": {
            "#import": [ "centos.7", "centos-x64", "rhel.7-x64" ]
        },

        "debian": {
            "#import": [ "linux" ]
        },
        "debian-x64": {
            "#import": [ "debian", "linux-x64" ]
        },
        "debian-x86": {
            "#import": [ "debian", "linux-x86" ]
        },
        "debian-arm": {
            "#import": [ "debian", "linux-arm" ]
        },
        "debian-armel": {
            "#import": [ "debian", "linux-armel" ]
        },
        "debian-arm64": {
            "#import": [ "debian", "linux-arm64" ]
        },

        "debian.8": {
            "#import": [ "debian" ]
        },
        "debian.8-x64": {
            "#import": [ "debian.8", "debian-x64" ]
        },
        "debian.8-x86": {
            "#import": [ "debian.8", "debian-x86" ]
        },
        "debian.8-arm": {
            "#import": [ "debian.8", "debian-arm" ]
        },
        "debian.8-armel": {
            "#import": [ "debian.8", "debian-armel" ]
        },
        "debian.8-arm64": {
            "#import": [ "debian.8", "debian-arm64" ]
        },

        "tizen": {
            "#import": [ "linux" ]
        },
        "tizen-armel": {
            "#import": [ "tizen", "linux-armel" ]
        },
        "tizen.4.0.0-armel": {
            "#import": [ "tizen.4.0.0", "tizen-armel" ]
        },

        "ubuntu": {
            "#import": [ "debian" ]
        },

        "ubuntu-x64": {
            "#import": [ "ubuntu", "debian-x64" ]
        },

        "ubuntu-x86": {
            "#import": [ "ubuntu", "debian-x86" ]
        },

        "ubuntu-arm": {
            "#import": [ "ubuntu", "debian-arm" ]
        },

        "ubuntu-arm64": {
            "#import": [ "ubuntu", "debian-arm64" ]
        },

        "ubuntu.14.04": {
            "#import": [ "ubuntu" ]
        },
        "ubuntu.14.04-x64": {
            "#import": [ "ubuntu.14.04", "ubuntu-x64" ]
        },
        "ubuntu.14.04-x86": {
            "#import": [ "ubuntu.14.04", "ubuntu-x86" ]
        },
        "ubuntu.14.04-arm": {
            "#import": [ "ubuntu.14.04", "ubuntu-arm" ]
        },

        "ubuntu.14.10": {
            "#import": [ "ubuntu" ]
        },
        "ubuntu.14.10-x64": {
            "#import": [ "ubuntu.14.10", "ubuntu-x64" ]
        },
        "ubuntu.14.10-x86": {
            "#import": [ "ubuntu.14.10", "ubuntu-x86" ]
        },
        "ubuntu.14.10-arm": {
            "#import": [ "ubuntu.14.10", "ubuntu-arm" ]
        },

        "ubuntu.15.04": {
            "#import": [ "ubuntu" ]
        },
        "ubuntu.15.04-x64": {
            "#import": [ "ubuntu.15.04", "ubuntu-x64" ]
        },
        "ubuntu.15.04-x86": {
            "#import": [ "ubuntu.15.04", "ubuntu-x86" ]
        },
        "ubuntu.15.04-arm": {
            "#import": [ "ubuntu.15.04", "ubuntu-arm" ]
        },

        "ubuntu.15.10": {
            "#import": [ "ubuntu" ]
        },
        "ubuntu.15.10-x64": {
            "#import": [ "ubuntu.15.10", "ubuntu-x64" ]
        },
        "ubuntu.15.10-x86": {
            "#import": [ "ubuntu.15.10", "ubuntu-x86" ]
        },
        "ubuntu.15.10-arm": {
            "#import": [ "ubuntu.15.10", "ubuntu-arm" ]
        },

        "ubuntu.16.04": {
            "#import": [ "ubuntu" ]
        },
        "ubuntu.16.04-x64": {
            "#import": [ "ubuntu.16.04", "ubuntu-x64" ]
        },
        "ubuntu.16.04-x86": {
            "#import": [ "ubuntu.16.04", "ubuntu-x86" ]
        },
        "ubuntu.16.04-arm": {
            "#import": [ "ubuntu.16.04", "ubuntu-arm" ]
        },
        "ubuntu.16.04-arm64": {
            "#import": [ "ubuntu.16.04", "ubuntu-arm64" ]
        },

        "ubuntu.16.10": {
            "#import": [ "ubuntu" ]
        },
        "ubuntu.16.10-x64": {
            "#import": [ "ubuntu.16.10", "ubuntu-x64" ]
        },
        "ubuntu.16.10-x86": {
            "#import": [ "ubuntu.16.10", "ubuntu-x86" ]
        },
        "ubuntu.16.10-arm": {
            "#import": [ "ubuntu.16.10", "ubuntu-arm" ]
        },
        "ubuntu.16.10-arm64": {
            "#import": [ "ubuntu.16.10", "ubuntu-arm64" ]
        },

        "linuxmint.17": {
            "#import": [ "ubuntu.14.04" ]
        },
        "linuxmint.17-x64": {
            "#import": [ "linuxmint.17", "ubuntu.14.04-x64" ]
        },

        "linuxmint.17.1": {
            "#import": [ "linuxmint.17" ]
        },
        "linuxmint.17.1-x64": {
            "#import": [ "linuxmint.17.1", "linuxmint.17-x64" ]
        },

        "linuxmint.17.2": {
            "#import": [ "linuxmint.17.1" ]
        },
        "linuxmint.17.2-x64": {
            "#import": [ "linuxmint.17.2", "linuxmint.17.1-x64" ]
        },

        "linuxmint.17.3": {
            "#import": [ "linuxmint.17.2" ]
        },
        "linuxmint.17.3-x64": {
            "#import": [ "linuxmint.17.3", "linuxmint.17.2-x64" ]
        },

        "linuxmint.18": {
            "#import": [ "ubuntu.16.04" ]
        },
        "linuxmint.18-x64": {
            "#import": [ "linuxmint.18", "ubuntu.16.04-x64" ]
        },
        "linuxmint.18.1": {
            "#import": [ "linuxmint.18" ]
        },
        "linuxmint.18.1-x64": {
            "#import": [ "linuxmint.18.1", "linuxmint.18-x64" ]
        },

        "fedora": {
            "#import": [ "linux" ]
        },
        "fedora-x64": {
            "#import": [ "fedora", "linux-x64" ]
        },

        "fedora.23": {
            "#import": [ "fedora" ]
        },
        "fedora.23-x64": {
            "#import": [ "fedora.23", "fedora-x64" ]
        },

        "fedora.24": {
            "#import": [ "fedora" ]
        },
        "fedora.24-x64": {
            "#import": [ "fedora.24", "fedora-x64" ]
        },

        "fedora.25": {
            "#import": [ "fedora" ]
        },
        "fedora.25-x64": {
            "#import": [ "fedora.25", "fedora-x64" ]
        },

        "fedora.26": {
            "#import": [ "fedora" ]
        },
        "fedora.26-x64": {
            "#import": [ "fedora.26", "fedora-x64" ]
        },

        "opensuse": {
            "#import": [ "linux" ]
        },
        "opensuse-x64": {
            "#import": [ "opensuse", "linux-x64" ]
        },

        "opensuse.13.2": {
            "#import": [ "opensuse" ]
        },
        "opensuse.13.2-x64": {
            "#import": [ "opensuse.13.2", "opensuse-x64" ]
        },

        "opensuse.42.1": {
            "#import": [ "opensuse" ]
        },
        "opensuse.42.1-x64": {
            "#import": [ "opensuse.42.1", "opensuse-x64" ]
        },

        "corert": {
            "#import": [ "any" ]
        },

        "win-corert": {
            "#import": [ "corert", "win" ]
        },
        "win-x86-corert": {
            "#import": [ "win-corert", "win-x86" ]
        },
        "win-x64-corert": {
            "#import": [ "win-corert", "win-x64" ]
        },

        "win7-corert": {
            "#import": [ "win-corert", "win7" ]
        },
        "win7-x86-corert": {
            "#import": [ "win7-corert", "win7-x86" ]
        },
        "win7-x64-corert": {
            "#import": [ "win7-corert", "win7-x64" ]
        },

        "win8-corert": {
            "#import": [ "win7-corert", "win8" ]
        },
        "win8-x86-corert": {
            "#import": [ "win8-corert", "win7-x86-corert", "win8-x86" ]
        },
        "win8-x64-corert": {
            "#import": [ "win8-corert", "win7-x64-corert", "win8-x64" ]
        },
        "win8-arm-corert": {
            "#import": [ "win8-corert", "win8-arm" ]
        },

        "win81-corert": {
            "#import": [ "win8-corert", "win81" ]
        },
        "win81-x86-corert": {
            "#import": [ "win81-corert", "win8-x86-corert", "win81-x86" ]
        },
        "win81-x64-corert": {
            "#import": [ "win81-corert", "win8-x64-corert", "win81-x64" ]
        },
        "win81-arm-corert": {
            "#import": [ "win81-corert", "win8-arm-corert", "win81-arm" ]
        },

        "win10-corert": {
            "#import": [ "win81-corert", "win10" ]
        },
        "win10-x86-corert": {
            "#import": [ "win10-corert", "win81-x86-corert", "win10-x86" ]
        },
        "win10-x64-corert": {
            "#import": [ "win10-corert", "win81-x64-corert", "win10-x64" ]
        },
        "win10-arm-corert": {
            "#import": [ "win10-corert", "win81-arm-corert", "win10-arm" ]
        },
        "win10-arm64-corert": {
            "#import": [ "win10-corert", "win10-arm64" ]
        },

        "unix-corert": {
            "#import": [ "corert", "unix" ]
        },
        "unix-x64-corert": {
            "#import": [ "unix-corert", "unix-x64" ]
        },
        "unix-arm-corert": {
            "#import": [ "unix-corert", "unix-arm" ]
        },
        "unix-arm64-corert": {
            "#import": [ "unix-corert", "unix-arm64" ]
        },

        "osx-corert": {
            "#import": [ "unix-corert", "osx" ]
        },
        "osx-x64-corert": {
            "#import": [ "osx-corert", "unix-x64-corert", "osx-x64" ]
        },

        "osx.10.10-corert": {
            "#import": [ "osx-corert", "osx.10.10" ]
        },
        "osx.10.10-x64-corert": {
            "#import": [ "osx.10.10-corert", "osx-x64-corert", "osx.10.10-x64" ]
        },

        "osx.10.11-corert": {
            "#import": [ "osx.10.10-corert", "osx.10.11" ]
        },
        "osx.10.11-x64-corert": {
            "#import": [ "osx.10.11-corert", "osx.10.10-x64-corert", "osx.10.11-x64" ]
        },

        "osx.10.12-corert": {
            "#import": [ "osx.10.11-corert", "osx.10.12" ]
        },
        "osx.10.12-x64-corert": {
            "#import": [ "osx.10.12-corert", "osx.10.11-x64-corert", "osx.10.12-x64" ]
        },

        "linux-corert": {
            "#import": [ "corert", "linux", "unix-corert" ]
        },
        "linux-x64-corert": {
            "#import": [ "linux-corert", "linux-x64" ]
        },
        "linux-arm-corert": {
            "#import": [ "linux-corert", "linux-arm" ]
        },
        "linux-arm64-corert": {
            "#import": [ "linux-corert", "linux-arm64" ]
        },

        "rhel-corert": {
            "#import": [ "corert", "rhel" ]
        },
        "rhel-x64-corert": {
            "#import": [ "rhel-corert", "linux-x64-corert", "rhel-x64" ]
        },

        "rhel.7-corert": {
            "#import": [ "rhel-corert", "rhel.7" ]
        },
        "rhel.7-x64-corert": {
            "#import": [ "rhel.7-corert", "rhel-x64-corert", "rhel.7-x64" ]
        },

        "rhel.7.0-corert": {
            "#import": [ "rhel.7-corert", "rhel.7.0" ]
        },
        "rhel.7.0-x64-corert": {
            "#import": [ "rhel.7.0-corert", "rhel.7-x64-corert", "rhel.7.0-x64" ]
        },

        "rhel.7.1-corert": {
            "#import": [ "rhel.7.0-corert", "rhel.7.1" ]
        },
        "rhel.7.1-x64-corert": {
            "#import": [ "rhel.7.1-corert", "rhel.7.0-x64-corert", "rhel.7.1-x64" ]
        },

        "rhel.7.2-corert": {
            "#import": [ "rhel.7.1-corert", "rhel.7.2" ]
        },
        "rhel.7.2-x64-corert": {
            "#import": [ "rhel.7.2-corert", "rhel.7.1-x64-corert", "rhel.7.2-x64" ]
        },

        "ol-corert": {
            "#import": [ "rhel-corert", "ol" ]
        },
        "ol-x64-corert": {
            "#import": [ "ol-corert", "rhel-x64-corert", "ol-x64" ]
        },

        "ol.7-corert": {
            "#import": [ "ol-corert", "ol.7" ]
        },
        "ol.7-x64-corert": {
            "#import": [ "ol.7-corert", "rhel.7-x64-corert", "ol.7-x64" ]
        },

        "ol.7.0-corert": {
            "#import": [ "ol.7-corert", "ol.7.0" ]
        },
        "ol.7.0-x64-corert": {
            "#import": [ "ol.7.0-corert", "rhel.7.0-corert", "ol.7.0-x64" ]
        },

        "ol.7.1-corert": {
            "#import": [ "ol.7.0-corert", "ol.7.1" ]
        },
        "ol.7.1-x64-corert": {
            "#import": [ "ol.7.1-corert", "rhel.7.1-x64-corert", "ol.7.1-x64" ]
        },

        "centos-corert": {
            "#import": [ "rel-corert", "centos" ]
        },
        "centos-x64-corert": {
            "#import": [ "centos-corert", "rhel-x64-corert", "centos-x64" ]
        },

        "centos.7-corert": {
            "#import": [ "centos-corert", "centos.7" ]
        },
        "centos.7-x64-corert": {
            "#import": [ "centos.7-corert", "centos-x64-corert", "centos.7-x64" ]
        },

        "debian-corert": {
            "#import": [ "linux-corert", "debian" ]
        },
        "debian-x64-corert": {
            "#import": [ "debian-corert", "linux-x64-corert", "debian-x64" ]
        },
        "debian-arm-corert": {
            "#import": [ "debian-corert", "debian-arm" ]
        },
        "debian-arm64-corert": {
            "#import": [ "debian-corert", "debian-arm64" ]
        },

        "debian.8-corert": {
            "#import": [ "debian-corert", "debian.8" ]
        },
        "debian.8-x64-corert": {
            "#import": [ "debian.8-corert", "debian-x64-corert", "debian.8-x64" ]
        },
        "debian.8-arm-corert": {
            "#import": [ "debian.8-corert", "debian-arm-corert", "debian.8-arm" ]
        },
        "debian.8-arm64-corert": {
            "#import": [ "debian.8-corert", "debian-arm64-corert", "debian.8-arm64" ]
        },

        "ubuntu-corert": {
            "#import": [ "debian-corert", "ubuntu" ]
        },

        "ubuntu-x64-corert": {
            "#import": [ "ubuntu-corert", "debian-x64-corert", "ubuntu-x64" ]
        },

        "ubuntu.14.04-corert": {
            "#import": [ "ubuntu-corert", "ubuntu.14.06" ]
        },
        "ubuntu.14.04-x64-corert": {
            "#import": [ "ubuntu.14.04-corert", "ubuntu-x64-corert", "ubuntu-14.04-x64" ]
        },

        "ubuntu.14.10-corert": {
            "#import": [ "ubuntu.14.04-corert", "ubuntu-14.10" ]
        },
        "ubuntu.14.10-x64-corert": {
            "#import": [ "ubuntu.14.10-corert", "ubuntu.14.04-x64-corert", "ubuntu.14.10-x64" ]
        },

        "ubuntu.15.04-corert": {
            "#import": [ "ubuntu.14.10-corert", "ubuntu-15.04" ]
        },
        "ubuntu.15.04-x64-corert": {
            "#import": [ "ubuntu.15.04-corert", "ubuntu.14.10-x64-corert", "ubuntu.15.04-x64" ]
        },

        "ubuntu.15.10-corert": {
            "#import": [ "ubuntu.15.04-corert", "ubuntu-15.10" ]
        },
        "ubuntu.15.10-x64-corert": {
            "#import": [ "ubuntu.15.10-corert", "ubuntu.15.04-x64-corert", "ubuntu.15.10-x64" ]
        },

        "ubuntu.16.04-corert": {
            "#import": [ "ubuntu.15.10-corert", "ubuntu-16.04" ]
        },
        "ubuntu.16.04-x64-corert": {
            "#import": [ "ubuntu.16.04-corert", "ubuntu.15.10-x64-corert", "ubuntu.16.04-x64" ]
        },

        "ubuntu.16.10-corert": {
            "#import": [ "ubuntu.16.04-corert", "ubuntu.16.10" ]
        },
        "ubuntu.16.10-x64-corert": {
            "#import": [ "ubuntu.16.10-corert", "ubuntu.16.04-x64-corert", "ubuntu.16.10-x64" ]
        },

        "linuxmint.17-corert": {
            "#import": [ "ubuntu.14.04-corert", "linuxmint.17" ]
        },
        "linuxmint.17-x64-corert": {
            "#import": [ "linuxmint.17-corert", "ubuntu.14.04-x64-corert", "linuxmint.17-x64" ]
        },

        "linuxmint.17.1-corert": {
            "#import": [ "linuxmint.17-corert", "linuxmint.17.1" ]
        },
        "linuxmint.17.1-x64-corert": {
            "#import": [ "linuxmint.17.1-corert", "linuxmint.17-x64-corert", "linuxmint.17.1-x64" ]
        },

        "linuxmint.17.2-corert": {
            "#import": [ "linuxmint.17.1-corert", "linuxmint.17.2" ]
        },
        "linuxmint.17.2-x64-corert": {
            "#import": [ "linuxmint.17.2-corert", "linuxmint.17.1-x64-corert", "linuxmint.17.2-x64" ]
        },

        "linuxmint.17.3-corert": {
            "#import": [ "linuxmint.17.2-corert", "linuxmint.17.3" ]
        },
        "linuxmint.17.3-x64-corert": {
            "#import": [ "linuxmint.17.3-corert", "linuxmint.17.2-x64-corert", "linuxmint.17.3-x64" ]
        },

        "linuxmint.18-corert": {
            "#import": [ "ubuntu.16.04-corert", "linuxmint.18" ]
        },
        "linuxmint.18-x64-corert": {
            "#import": [ "linuxmint.18-corert", "ubuntu.16.04-x64-corert", "linuxmint.18-x64" ]
        },

        "fedora-corert": {
            "#import": [ "linux-corert", "fedora" ]
        },
        "fedora-x64-corert": {
            "#import": [ "fedora-corert", "linux-x64-corert", "fedora-x64" ]
        },

        "fedora.23-corert": {
            "#import": [ "fedora-corert", "fedora.23" ]
        },
        "fedora.23-x64-corert": {
            "#import": [ "fedora.23-corert", "fedora-x64-corert", "fedora.23-x64" ]
        },

        "fedora.24-corert": {
            "#import": [ "fedora.23-corert", "fedora.24" ]
        },
        "fedora.24-x64-corert": {
            "#import": [ "fedora.24-corert", "fedora.23-x64-corert", "fedora.24-x64" ]
        },

        "opensuse-corert": {
            "#import": [ "linux-corert", "opensuse" ]
        },
        "opensuse-x64-corert": {
            "#import": [ "opensuse-corert", "linux-x64-corert", "opensuste-x64" ]
        },

        "opensuse.13.2-corert": {
            "#import": [ "opensuse-corert", "opensuse.13.2" ]
        },
        "opensuse.13.2-x64-corert": {
            "#import": [ "opensuse.13.2-corert", "opensuse-x64-corert", "opensuse.13.2-x64" ]
        },

        "opensuse.42.1-corert": {
            "#import": [ "opensuse.13.2-corert", "opensuse.42.1" ]
        },
        "opensuse.42.1-x64-corert": {
            "#import": [ "opensuse.42.1-corert", "opensuse.13.2-x64-corert", "opensuse.42.1-x64" ]
        },

    }
}
"""


[<Test>]
let ``Check if we can parse runtime support and runtime dependencies``() =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph supportAndDeps

    runtimeGraph
    |> shouldEqual
        { Runtimes =
            [ { Rid = Rid.Of "win"; InheritedRids = [ ]
                RuntimeDependencies =
                  [ PackageName "Microsoft.Win32.Primitives", [ PackageName "runtime.win.Microsoft.Win32.Primitives", VersionRequirement.VersionRequirement (VersionRange.Minimum (SemVer.Parse "4.3.0"), PreReleaseStatus.No) ]
                    PackageName "System.Runtime.Extensions", [ PackageName "runtime.win.System.Runtime.Extensions", VersionRequirement.VersionRequirement (VersionRange.Minimum (SemVer.Parse "4.3.0"), PreReleaseStatus.No) ]
                  ]
                  |> Map.ofSeq } ]
            |> Seq.map (fun r -> r.Rid, r)
            |> Map.ofSeq
          Supports =
            [ { Name = "net45.app"
                Supported =
                  [ FrameworkIdentifier.DotNetFramework FrameworkVersion.V4_5, [ Rid.Of ""; Rid.Of "win-x86"; Rid.Of "win-x64" ]]
                  |> Map.ofSeq }
              { Name = "uwp.10.0.app"
                Supported =
                  [ FrameworkIdentifier.UAP UAPVersion.V10, [ Rid.Of "win10-x86"; Rid.Of "win10-x86-aot"; Rid.Of "win10-arm"; Rid.Of "win10-arm-aot" ]]
                  |> Map.ofSeq } ]
            |> Seq.map (fun c -> c.Name, c)
            |> Map.ofSeq
        }

[<Test>]
let ``Check if we can parse runtime rids``() =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph rids

    runtimeGraph
    |> shouldEqual
        { Runtimes =
            [ { Rid = Rid.Of "base"; InheritedRids = [ ]; RuntimeDependencies = Map.empty }
              { Rid = Rid.Of "any"; InheritedRids = [ Rid.Of "base" ]; RuntimeDependencies = Map.empty }
              { Rid = Rid.Of "win"; InheritedRids = [ Rid.Of "any" ]; RuntimeDependencies = Map.empty }
              { Rid = Rid.Of "win-x86"; InheritedRids = [ Rid.Of "win" ]; RuntimeDependencies = Map.empty } ]
            |> Seq.map (fun r -> r.Rid, r)
            |> Map.ofSeq
          Supports =
            []
            |> Map.ofSeq
        }

[<Test>]
let ``Check if we can merge two graphs``() =
    let r1 = RuntimeGraphParser.readRuntimeGraph rids
    let r2 = RuntimeGraphParser.readRuntimeGraph supportAndDeps
    let merged = RuntimeGraph.merge r1 r2
    let win = merged.Runtimes.[Rid.Of "win"]
    win.InheritedRids
        |> shouldEqual [ Rid.Of "any" ]
    win.RuntimeDependencies
        |> shouldEqual
             ([ PackageName "Microsoft.Win32.Primitives", [ PackageName "runtime.win.Microsoft.Win32.Primitives", VersionRequirement.VersionRequirement (VersionRange.Minimum (SemVer.Parse "4.3.0"), PreReleaseStatus.No) ]
                PackageName "System.Runtime.Extensions", [ PackageName "runtime.win.System.Runtime.Extensions", VersionRequirement.VersionRequirement (VersionRange.Minimum (SemVer.Parse "4.3.0"), PreReleaseStatus.No) ]
              ] |> Map.ofSeq)

[<Test>]
let ``Check that runtime dependencies are saved as such in the lockfile`` () =
    let lockFileData = """ """
    let getLockFile lockFileData = LockFile.Parse("",toLines lockFileData)
    let lockFile = lockFileData |> getLockFile

    let graph =
        [ "MyDependency", "3.2.0", [], RuntimeGraph.Empty
          "MyDependency", "3.3.3", [], RuntimeGraph.Empty
          "MyDependency", "4.0.0", [], RuntimeGraphParser.readRuntimeGraph """{
  "runtimes": {
    "win": {
      "MyDependency": {
        "MyRuntimeDependency": "4.0.0"
      }
    }
  }
}"""
          "MyRuntimeDependency", "4.0.0", [], RuntimeGraph.Empty
          "MyRuntimeDependency", "4.0.1", [], RuntimeGraph.Empty ]
        |> OfGraphWithRuntimeDeps

    let expectedLockFile = """NUGET
  remote: http://www.nuget.org/api/v2
    MyDependency (4.0)
    MyRuntimeDependency (4.0.1) - isRuntimeDependency: true"""

    let depsFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2
nuget MyDependency""")
    let lockFile, resolution =
        UpdateProcess.selectiveUpdate true noSha1 (VersionsFromGraph graph) (PackageDetailsFromGraph graph) (GetRuntimeGraphFromGraph graph) lockFile depsFile PackageResolver.UpdateMode.Install SemVerUpdateMode.NoRestriction

    let result =
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version, resolved.IsRuntimeDependency))

    let expected =
        [("MyDependency","4.0.0", false);
        ("MyRuntimeDependency","4.0.1", true)]
        |> Seq.sortBy (fun (t,_,_) ->t)

    result
    |> Seq.sortBy (fun (t,_,_) ->t)
    |> shouldEqual expected

    lockFile.GetGroup(Constants.MainDependencyGroup).Resolution
    |> LockFileSerializer.serializePackages depsFile.Groups.[Constants.MainDependencyGroup].Options
    |> shouldEqual (normalizeLineEndings expectedLockFile)

[<Test>]
let ``Check that runtime dependencies we don't use are ignored`` () =
    let lockFileData = """ """
    let getLockFile lockFileData = LockFile.Parse("",toLines lockFileData)
    let lockFile = lockFileData |> getLockFile

    let graph =
        [ "MyDependency", "3.2.0", [], RuntimeGraph.Empty
          "MyDependency", "3.3.3", [], RuntimeGraph.Empty
          "MyDependency", "4.0.0", [], RuntimeGraphParser.readRuntimeGraph """{
  "runtimes": {
    "win": {
      "SomePackage": {
        "MyRuntimeDependency": "4.0.0"
      }
    }
  }
}"""
          "MyRuntimeDependency", "4.0.0", [], RuntimeGraph.Empty
          "MyRuntimeDependency", "4.0.1", [], RuntimeGraph.Empty ]
        |> OfGraphWithRuntimeDeps

    let depsFile = DependenciesFile.FromSource("""source http://www.nuget.org/api/v2
nuget MyDependency""")
    let lockFile, resolution =
        UpdateProcess.selectiveUpdate true noSha1 (VersionsFromGraph graph) (PackageDetailsFromGraph graph) (GetRuntimeGraphFromGraph graph) lockFile depsFile PackageResolver.UpdateMode.Install SemVerUpdateMode.NoRestriction

    let result =
        lockFile.GetGroupedResolution()
        |> Seq.map (fun (KeyValue (_,resolved)) -> (string resolved.Name, string resolved.Version, resolved.IsRuntimeDependency))

    let expected =
        [("MyDependency","4.0.0", false)]
        |> Seq.sortBy (fun (t,_,_) ->t)

    result
    |> Seq.sortBy (fun (t,_,_) ->t)
    |> shouldEqual expected

[<Test>]
let ``Check that runtime dependencies are loaded from the lockfile`` () =
    let lockFile = """NUGET
  remote: http://www.nuget.org/api/v2
    MyDependency (4.0)
    MyRuntimeDependency (4.0.1) - isRuntimeDependency: true"""

    let lockFile = LockFileParser.Parse(toLines lockFile) |> List.head
    let packages = List.rev lockFile.Packages

    let expected =
        [("MyDependency","4.0", false);
        ("MyRuntimeDependency","4.0.1", true)]
        |> List.sortBy (fun (t,_,_) ->t)

    packages
    |> List.map (fun r -> string r.Name, string r.Version, r.IsRuntimeDependency)
    |> List.sortBy (fun (t,_,_) ->t)
    |> shouldEqual expected

    
[<Test>]
let ``Check that runtime inheritance works`` () =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph rids
    let content =
        { NuGet.NuGetPackageContent.Path = "/c/test/blub";
          NuGet.NuGetPackageContent.Spec = Nuspec.All
          NuGet.NuGetPackageContent.Content =
            NuGet.ofFiles [
              "lib/netstandard1.1/testpackage.xml"
              "lib/netstandard1.1/testpackage.dll"
              "runtimes/win/lib/netstandard1.1/testpackage.xml"
              "runtimes/win/lib/netstandard1.1/testpackage.dll"
            ]}
    let model =
        InstallModel.EmptyModel (PackageName "testpackage", SemVer.Parse "1.0.0")
        |> InstallModel.addNuGetFiles content
        
    let targetProfile = Paket.TargetProfile.SinglePlatform(Paket.FrameworkIdentifier.DotNetStandard (Paket.DotNetStandardVersion.V1_6))
    model.GetRuntimeAssemblies runtimeGraph (Rid.Of "win-x86") (targetProfile)
    |> Seq.map (fun fi -> fi.Library.PathWithinPackage)
    |> Seq.toList
    |> shouldEqual [ "runtimes/win/lib/netstandard1.1/testpackage.dll" ]
    
[<Test>]
let ``Check that runtime inheritance works (2)`` () =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph completeRids
    let content =
        { NuGet.NuGetPackageContent.Path = "~/.nuget/packages/System.Runtime.InteropServices.RuntimeInformation";
          NuGet.NuGetPackageContent.Spec = Nuspec.All
          NuGet.NuGetPackageContent.Content =
            NuGet.ofFiles [
              "lib/MonoTouch10/_._"
              "lib/net45/System.Runtime.InteropServices.RuntimeInformation.dll"
              "lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll"
              "ref/MonoTouch10/_._"
              "ref/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/aot/lib/netcore50/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/unix/lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/win/lib/net45/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/win/lib/netcore50/System.Runtime.InteropServices.RuntimeInformation.dll"
              "runtimes/win/lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll"
            ]}
    let model =
        InstallModel.EmptyModel (PackageName "System.Runtime.InteropServices.RuntimeInformation", SemVer.Parse "4.3.0")
        |> InstallModel.addNuGetFiles content
        
    let targetProfile = Paket.TargetProfile.SinglePlatform(Paket.FrameworkIdentifier.DotNetStandard (Paket.DotNetStandardVersion.V1_6))
    model.GetRuntimeAssemblies runtimeGraph (Rid.Of "win10-x86") (targetProfile)
    |> Seq.map (fun fi -> fi.Library.PathWithinPackage)
    |> Seq.toList
    |> shouldEqual [ "runtimes/win/lib/netstandard1.1/System.Runtime.InteropServices.RuntimeInformation.dll" ]

[<Test>]
let ``Check correct inheritance list`` () =
    let runtimeGraph = RuntimeGraphParser.readRuntimeGraph rids
    RuntimeGraph.getInheritanceList (Rid.Of "win-x86") runtimeGraph
        |> shouldEqual [ Rid.Of "win-x86"; Rid.Of "win"; Rid.Of "any"; Rid.Of "base"]
    