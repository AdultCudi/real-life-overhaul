using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

internal static partial class Program
{
    // Verify the public getter's executable body, rather than assuming a property is its serialized field.
    private static void CompanionPriceBasisChecks(string managed)
    {
        Section("companion registered prices: native fields before market multiplier");
        using (var pe = new PEReader(File.OpenRead(Path.Combine(managed, "BigAmbitions.Items.dll"))))
        {
            MetadataReader md = pe.GetMetadataReader();
            TypeDefinition item = md.GetTypeDefinition(md.TypeDefinitions.First(handle =>
                md.GetString(md.GetTypeDefinition(handle).Namespace) == "BigAmbitions.Items"
                && md.GetString(md.GetTypeDefinition(handle).Name) == "Item"));
            FieldDefinitionHandle market = item.GetFields().First(handle => md.GetString(md.GetFieldDefinition(handle).Name) == "defaultMarketPrice");
            FieldDefinitionHandle wholesale = item.GetFields().First(handle => md.GetString(md.GetFieldDefinition(handle).Name) == "wholesalePrice");
            MethodDefinition getter = md.GetMethodDefinition(item.GetMethods().First(handle =>
                md.GetString(md.GetMethodDefinition(handle).Name) == "get_DefaultMarketPrice"));
            byte[] body = pe.GetMethodBody(getter.RelativeVirtualAddress).GetILBytes();
            bool direct = body.Length == 7 && body[0] == 0x02 && body[1] == 0x7b && body[6] == 0x2a
                && BitConverter.ToInt32(body, 2) == MetadataTokens.GetToken(market);
            Check(direct, "companion: Item.DefaultMarketPrice directly returns defaultMarketPrice without a market multiplier");
            Check((md.GetFieldDefinition(wholesale).Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public,
                "companion: Item.wholesalePrice remains the public registered field");
            // Interpret the verified three-instruction getter at a non-default multiplier. No game assembly is loaded.
            float defaultMarketPrice = 10f, marketPriceMultiplier = 1.3f;
            float captured = direct ? defaultMarketPrice : float.NaN;
            Check(captured == 10f && captured != defaultMarketPrice * marketPriceMultiplier,
                "companion: multiplier 1.3 does not enter the registered default-market getter");
        }
    }
}
