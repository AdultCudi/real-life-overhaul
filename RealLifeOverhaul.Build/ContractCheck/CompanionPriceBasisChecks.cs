using System;
using System.IO;
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
            TypeDefinitionHandle? found = null;
            foreach (TypeDefinitionHandle handle in md.TypeDefinitions)
            {
                TypeDefinition type = md.GetTypeDefinition(handle);
                if (md.GetString(type.Namespace) == "BigAmbitions.Items" && md.GetString(type.Name) == "Item")
                {
                    found = handle;
                    break;
                }
            }
            // A game update that renames any of these prints a failed line, as every other contract line does.
            Check(found != null, "companion: BigAmbitions.Items.Item exists");
            if (found == null)
            {
                return;
            }
            TypeDefinition item = md.GetTypeDefinition(found.Value);
            FieldDefinitionHandle? market = null, wholesale = null;
            foreach (FieldDefinitionHandle handle in item.GetFields())
            {
                string name = md.GetString(md.GetFieldDefinition(handle).Name);
                if (name == "defaultMarketPrice")
                {
                    market = handle;
                }
                else if (name == "wholesalePrice")
                {
                    wholesale = handle;
                }
            }
            MethodDefinitionHandle? getterHandle = null;
            foreach (MethodDefinitionHandle handle in item.GetMethods())
            {
                if (md.GetString(md.GetMethodDefinition(handle).Name) == "get_DefaultMarketPrice")
                {
                    getterHandle = handle;
                    break;
                }
            }
            Check(market != null, "companion: Item.defaultMarketPrice exists");
            Check(wholesale != null, "companion: Item.wholesalePrice exists");
            Check(getterHandle != null, "companion: Item.DefaultMarketPrice has a getter");
            if (market == null || wholesale == null || getterHandle == null)
            {
                return;
            }
            MethodDefinition getter = md.GetMethodDefinition(getterHandle.Value);
            byte[] body = pe.GetMethodBody(getter.RelativeVirtualAddress).GetILBytes();
            bool direct = body.Length == 7 && body[0] == 0x02 && body[1] == 0x7b && body[6] == 0x2a
                && BitConverter.ToInt32(body, 2) == MetadataTokens.GetToken(market.Value);
            Check(direct, "companion: Item.DefaultMarketPrice directly returns defaultMarketPrice without a market multiplier");
            Check((md.GetFieldDefinition(wholesale.Value).Attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public,
                "companion: Item.wholesalePrice remains the public registered field");
            // Interpret the verified three-instruction getter at a non-default multiplier. No game assembly is loaded.
            float defaultMarketPrice = 10f, marketPriceMultiplier = 1.3f;
            float captured = direct ? defaultMarketPrice : float.NaN;
            Check(captured == 10f && captured != defaultMarketPrice * marketPriceMultiplier,
                "companion: multiplier 1.3 does not enter the registered default-market getter");
        }
    }
}
