namespace PaketTest2394.PackageB;

public static class PackageDescription
{
    public static string GetDescription()
    {
        var assemblyName = typeof(PackageDescription).Assembly.GetName();
        return $"PackageB {assemblyName.Version!.ToString(2)} (references {Transient.PackageDescription.GetDescription()})";
    }
}
