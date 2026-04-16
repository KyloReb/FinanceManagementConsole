using System; using System.Linq; using System.Reflection; using MudBlazor; class P { static void Main() { foreach(var p in typeof(MudChart).GetProperties()) Console.WriteLine(p.Name); } }
