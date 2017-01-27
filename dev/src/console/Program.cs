using System;

public class Product
{
    public string Name { get;set; }
    public DateTime ExpiryDate { get;set; }
    public decimal Price { get;set; }
    public string[] Sizes { get;set; }
}

public class Program
{
    static void Main(string[] args)
    {
        var product = new Product
        {
            Name = "Apple",
            ExpiryDate = new DateTime(2008, 12, 28),
            Price = 3.99M,
            Sizes = new string[] { "Small", "Medium", "Large" },
        };

        var output = JsonConvert.SerializeObject(product);

        Console.WriteLine("Hello World!");
        Console.WriteLine("Prova json: ${output}");
    }
}
