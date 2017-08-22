namespace NStore.Persistence.DocumentDb
{
    using System;

    public interface ISerializer
    {
        string Serialize(Object o);

        Object Deserialize(string text, Type t);
    }
}
