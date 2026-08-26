namespace PaketTest2394.PackageB.Transient;

public static class PackageDescription
{
    public static string GetDescription()
    {
        var assemblyName = typeof(PackageDescription).Assembly.GetName();
        return $"PackageB.Transient {assemblyName.Version!.ToString(2)}";
    }
}
