// --------------------------------------------------------------------------------------
// FAKE build script
// --------------------------------------------------------------------------------------

#r @"packages/build/FAKE/tools/FakeLib.dll"

open Fake
open System
open System.IO

let Exec command args =
    let result = Shell.Exec(command, args)
    if result <> 0 then failwithf "%s exited with error %d" command result

let ExecutePlistBuddy key value path =
    Exec "/usr/libexec/PlistBuddy" ("-c 'Set :" + key + " " + value + "' " + path)

Target "failing-test" (fun _ ->
    ExecutePlistBuddy "CFBundleIdentifier" "test1" "Some/Info.plist"
    // Remove the next line, and we're good to go.
    ExecutePlistBuddy "CFBundleDisplayName" "test2" "Some/Info.plist"
)

RunTargetOrDefault "failing-test"