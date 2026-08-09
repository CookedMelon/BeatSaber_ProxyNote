namespace BeatSaberMarkupLanguage.Attributes;

[System.AttributeUsage(System.AttributeTargets.Property)]
internal sealed class UIValueAttribute : System.Attribute
{
    internal UIValueAttribute(string name)
    {
    }
}

[System.AttributeUsage(System.AttributeTargets.Method)]
internal sealed class UIActionAttribute : System.Attribute
{
    internal UIActionAttribute(string name)
    {
    }
}
