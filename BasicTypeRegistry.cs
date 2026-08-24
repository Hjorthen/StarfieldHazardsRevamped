using System;
using System.Collections.Generic;

public interface ITypeRegistry {
    T Resolve<T>() where T : class;
    bool TryResolve<T>(out T? result) where T : class;
    void Add<T>(T component) where T : class;
}


public class BasicTypeRegistry : ITypeRegistry {
    private readonly List<object> components = [];

    public T Resolve<T>() where T : class {
        if(TryResolve(out T? result)) {
            return result!;
        }
        
        throw new Exception("Could not find component of type " + typeof(T).Name);
    }

    public bool TryResolve<T>(out T? result) where T : class {
        foreach (var component in components)
        {
            if (component is T typed) {
                result = typed;
                return true;
            }
        }
        result = null;
        return false;
    }

    public void Add<T>(T component) where T : class {
        if(TryResolve<T>(out var _))  {
            throw new Exception($"Type {typeof(T).Name} already added as a component.");
        }

        components.Add(component);
    }
}