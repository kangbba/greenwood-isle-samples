
public enum DialogueCmdType
{
    Pause,
    Resume,
    CompleteCurrentLine,   // Reveal the rest of the current line.
    Exit,                  // Reveal everything that remains.
    Clear
}

public readonly struct DialogueCmd
{
    public readonly DialogueCmdType Type;

    public DialogueCmd(DialogueCmdType type)
    {
        Type = type;
    }

    public override string ToString() => $"DialogueCmd {{ {Type} }}";
}
