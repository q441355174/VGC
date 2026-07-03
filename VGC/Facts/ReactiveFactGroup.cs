using System.Collections.ObjectModel;
using ReactiveUI;

namespace VGC.Facts;

public sealed class ReactiveFactGroup : ReactiveObject
{
    private readonly Dictionary<string, Fact> _facts = [];
    private readonly ObservableCollection<Fact> _mutableFacts = [];

    public ReactiveFactGroup(string name)
    {
        Name = name;
        Facts = new ReadOnlyObservableCollection<Fact>(_mutableFacts);
    }

    public string Name { get; }

    public ReadOnlyObservableCollection<Fact> Facts { get; }

    public event EventHandler<Fact>? FactAdded;

    public event EventHandler<Fact>? FactRemoved;

    public Fact DefineFact(string key, FactValueType valueType, object? value = null, string? units = null, string? description = null)
    {
        var metadata = new FactMetaData(key, valueType)
        {
            Units = units,
            ShortDescription = description
        };
        var fact = new Fact(0, key, metadata, value);
        AddOrReplace(fact);
        return fact;
    }

    public void AddOrReplace(Fact fact)
    {
        if (_facts.TryGetValue(fact.Name, out var existing))
        {
            var index = _mutableFacts.IndexOf(existing);
            _mutableFacts[index] = fact;
        }
        else
        {
            _mutableFacts.Add(fact);
            FactAdded?.Invoke(this, fact);
        }

        _facts[fact.Name] = fact;
        this.RaisePropertyChanged(nameof(Facts));
    }

    public bool TryGet(string key, out Fact? fact)
    {
        return _facts.TryGetValue(key, out fact);
    }

    public bool Remove(string key)
    {
        if (!_facts.Remove(key, out var fact))
        {
            return false;
        }

        _mutableFacts.Remove(fact);
        FactRemoved?.Invoke(this, fact);
        this.RaisePropertyChanged(nameof(Facts));
        return true;
    }
}
