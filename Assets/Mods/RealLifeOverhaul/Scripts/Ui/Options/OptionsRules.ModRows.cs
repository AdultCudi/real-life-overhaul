using System.Reflection;

namespace RealLifeOverhaul.Ui
{
    // Reading a field of one of the game's controls: game-free, so the checks can try it on controls of the wrong type.
    public static partial class OptionsRules
    {
        /// <summary>Whether a field can be read on this owner: the owner is of the type that declares it.</summary>
        public static bool Holds(FieldInfo field, object owner) => field != null && owner != null && field.DeclaringType != null && field.DeclaringType.IsInstanceOfType(owner);

        /// <summary>The field's value on the owner; null when the field is missing or the owner is of another type.</summary>
        public static object Read(FieldInfo field, object owner) => Holds(field, owner) ? field.GetValue(owner) : null;
    }
}
