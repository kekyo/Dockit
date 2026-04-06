namespace Dockit.Internal;

internal enum DocumentationAccessibility
{
    Private = 0,
    PrivateProtected = 1,
    Internal = 2,
    Protected = 3,
    ProtectedInternal = 4,
    Public = 5,
}

internal enum DocumentationEditorBrowsableVisibility
{
    Normal = 0,
    Advanced = 1,
    Always = 2,
}

internal readonly struct DocumentationVisibilityOptions
{
    public DocumentationVisibilityOptions(
        DocumentationAccessibility accessibility,
        DocumentationEditorBrowsableVisibility editorBrowsableVisibility)
    {
        this.Accessibility = accessibility;
        this.EditorBrowsableVisibility = editorBrowsableVisibility;
    }

    public DocumentationAccessibility Accessibility { get; }

    public DocumentationEditorBrowsableVisibility EditorBrowsableVisibility { get; }

    public static DocumentationVisibilityOptions Default { get; } =
        new(
            DocumentationAccessibility.Protected,
            DocumentationEditorBrowsableVisibility.Advanced);
}
