[<AutoOpen>]
module Paket.PlatformDetection

open System
open System.IO

let isMonoRuntime =
    not (Object.ReferenceEquals(Type.GetType "Mono.Runtime", null))

/// Determines if the current system is an Unix system
let isUnix = Environment.OSVersion.Platform = PlatformID.Unix

/// Determines if the current system is a MacOs system
let isMacOS =
    (Environment.OSVersion.Platform = PlatformID.MacOSX) ||
        // Running on OSX with mono, Environment.OSVersion.Platform returns Unix
        // rather than MacOSX, so check for osascript (the AppleScript
        // interpreter). Checking for osascript for other platforms can cause a
        // problem on Windows if the current-directory is on a mapped-drive
        // pointed to a Mac's root partition; e.g., Parallels does this to give
        // Windows virtual machines access to files on the host.
        (Environment.OSVersion.Platform = PlatformID.Unix && (File.Exists "/usr/bin/osascript"))

/// Determines if the current system is a Linux system
let isLinux = int System.Environment.OSVersion.Platform |> fun p -> (p = 4) || (p = 6) || (p = 128)

/// Determines if the current system is a mono system
let isMono = isMonoRuntime || isLinux || isUnix || isMacOS

let isWindows =
    if isMono then false else
        Environment.OSVersion.Platform = PlatformID.Win32NT

let isWin8 = isWindows && Environment.OSVersion.Version >= new Version(6, 2, 9200, 0)