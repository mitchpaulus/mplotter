using System;
using System.Collections.Generic;
using Avalonia.Controls;
using ScottPlot;

namespace csvplot;

public class UiState<T>
{
     private T _value;
     private readonly Action<T> _updateAction;
     private readonly MainViewModel _viewModel;
     private readonly List<ISubscriber> _subscribers = new();
 
     public T Value => _value;
 
     public UiState(T initValue, Action<T> updateAction, MainViewModel viewModel)
     {
         _value = initValue;
         _updateAction = updateAction;
         _viewModel = viewModel;
         _updateAction(initValue);
     }
 
     public void Update(T newDateTime)
     {
         _value = newDateTime;
         _updateAction(newDateTime);
         foreach (var subscriber in _subscribers) subscriber.Update();
     }
 
     public void AddSubscriber(ISubscriber subscriber)
     {
         _subscribers.Add(subscriber);
     }   
}

public class DateTimeState
{
    private DateTime _dateTime;
    private readonly Action<DateTime> _updateAction;
    private readonly MainViewModel _viewModel;
    private readonly List<ISubscriber> _subscribers = new();

    public DateTime Value => _dateTime;

    public DateTimeState(DateTime dateTime, Action<DateTime> updateAction, MainViewModel viewModel)
    {
        _dateTime = dateTime;
        _updateAction = updateAction;
        _viewModel = viewModel;
        _updateAction(dateTime);
    }

    public void Update(DateTime newDateTime)
    {
        _dateTime = newDateTime;
        _updateAction(newDateTime);
        foreach (var subscriber in _subscribers) subscriber.Update();
    }

    public void AddSubscriber(ISubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }
}

public interface ISubscriber
{
    void Update();
}

public class ComputedDateTimeState : ISubscriber
{
    private readonly Func<MainViewModel, DateTime> _computedValue;
    private readonly Action<DateTime> _updateAction;
    private readonly MainViewModel _viewModel;
    private DateTime _currentValue;
    private readonly List<ISubscriber> _subscribers = new();
    
    public DateTime Value => _currentValue;
    
    public ComputedDateTimeState(Func<MainViewModel, DateTime> computedValue, Action<DateTime> updateAction, MainViewModel viewModel)
    {
        _computedValue = computedValue;
        _updateAction = updateAction;
        _viewModel = viewModel;
        _currentValue = computedValue(viewModel);
        _updateAction(_currentValue);
    }

    public void Update()
    {
        _currentValue = _computedValue(_viewModel);
        _updateAction(_currentValue);
        foreach (var s in _subscribers) s.Update();
    }
    
    public void AddSubscriber(ISubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }

    public void Update(MainViewModel viewModel)
    {
        throw new NotImplementedException();
    }
}

public class ArrayState<T> 
{
    private readonly List<T> _initialArray;
    private readonly MainViewModel _viewModel;
    
    private readonly List<ISubscriber> _subscribers = new();

    public List<T> Value => _initialArray;

    public ArrayState(List<T> initialArray, Action<T> addAction, Action clearAction, MainViewModel viewModel)
    {
        _initialArray = initialArray;
        _viewModel = viewModel;
    }
    
    public void Update(MainViewModel viewModel)
    {
        //
    }

    public void UpdateItem(int index, Action<T> update)
    {
        update(_initialArray[index]);
        foreach (var s in _subscribers) s.Update();
    }
    
    public void AddSubscriber(ISubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }
}

public class ComputedArray<T> : ISubscriber, ISubscribable
{
    private readonly Func<MainViewModel,List<T>> _computedValue;
    private readonly Action<List<T>> _updateAction;
    private readonly MainViewModel _viewModel;
    private readonly List<T> _currentValue;

    private readonly List<ISubscriber> _subscribers = new();

    public ComputedArray(Func<MainViewModel, List<T>> computedValue, Action<List<T>> updateAction, MainViewModel viewModel)
    {
        _computedValue = computedValue;
        _updateAction = updateAction;
        _viewModel = viewModel;
        _currentValue = computedValue(viewModel);
        _updateAction(_currentValue);
    }
    
    public void AddSubscriber(ISubscriber subscriber)
    {
        _subscribers.Add(subscriber);
    }

    public void Update()
    {
        var newValue = _computedValue(_viewModel);
        _updateAction(newValue);
        foreach (var s in _subscribers) s.Update();
    }
}

public interface ISubscribable
{
    void AddSubscriber(ISubscriber subscriber);
}