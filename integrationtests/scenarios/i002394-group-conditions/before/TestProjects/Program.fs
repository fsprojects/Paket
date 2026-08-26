// To verify versions of referenced packages
printfn "%s" (PaketTest2394.PackageA.PackageDescription.GetDescription())
printfn "%s" (PaketTest2394.PackageB.PackageDescription.GetDescription())
printfn "%s" (PaketTest2394.PackageB.Transient.PackageDescription.GetDescription())

// To verify that *.targets file is loaded from the correct package version
#if PACKAGEA_1
printfn "Constant PACKAGEA_1 set"
#endif
#if PACKAGEA_2
printfn "Constant PACKAGEA_2 set"
#endif
#if PACKAGEA_3
printfn "Constant PACKAGEA_3 set"
#endif
#if PACKAGEA_4
printfn "Constant PACKAGEA_4 set"
#endif
#if PACKAGEA_5
printfn "Constant PACKAGEA_5 set"
#endif
