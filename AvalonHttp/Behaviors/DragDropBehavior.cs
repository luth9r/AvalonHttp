using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using AvalonHttp.ViewModels;
using CollectionItemViewModel = AvalonHttp.ViewModels.CollectionAggregate.CollectionItemViewModel;
using RequestItemViewModel = AvalonHttp.ViewModels.CollectionAggregate.RequestItemViewModel;

namespace AvalonHttp.Behaviors;

public class DragDropBehavior : Behavior<Border>
{
    private Point _startPoint;
    private bool _isDragStart;
    
    private bool? _insertAfterState = null;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            DragDrop.SetAllowDrop(AssociatedObject, true);
            AssociatedObject.PointerPressed += OnPointerPressed;
            AssociatedObject.PointerMoved += OnPointerMoved;
            AssociatedObject.PointerReleased += OnPointerReleased;
            AssociatedObject.AddHandler(DragDrop.DragOverEvent, OnDragOver);
            AssociatedObject.AddHandler(DragDrop.DropEvent, OnDrop);
            AssociatedObject.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        }
    }

    protected override void OnDetaching()
    {
        base.OnDetaching();
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerPressed -= OnPointerPressed;
            AssociatedObject.PointerMoved -= OnPointerMoved;
            AssociatedObject.PointerReleased -= OnPointerReleased;
            AssociatedObject.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
            AssociatedObject.RemoveHandler(DragDrop.DropEvent, OnDrop);
            AssociatedObject.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        }
    }

    // --- Source Logic ---
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (properties.IsLeftButtonPressed)
        {
            if (e.Source is Control c && (c is TextBox || c is Button)) return;
            _startPoint = e.GetPosition(AssociatedObject);
            _isDragStart = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e) => _isDragStart = false;

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragStart || AssociatedObject == null) return;
        var point = e.GetCurrentPoint(AssociatedObject);
        if (!point.Properties.IsLeftButtonPressed)
        {
            _isDragStart = false;
            return;
        }

        var diff = e.GetPosition(AssociatedObject) - _startPoint;
        if (Math.Abs(diff.X) > 10 || Math.Abs(diff.Y) > 10)
        {
            _isDragStart = false;
            await StartDrag(e);
        }
    }

    private async Task StartDrag(PointerEventArgs e)
    {
        var context = AssociatedObject?.DataContext;
        if (context == null) return;

        var dragData = new DataObject();
        dragData.Set("Context", context);

        if (AssociatedObject != null) AssociatedObject.Opacity = 0.5;
        
        try { await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move); }
        catch { /* ignored */ }
        finally { 
            if (AssociatedObject != null) AssociatedObject.Opacity = 1.0; 
            _insertAfterState = null;
        }
    }

    // Target Logic with DEAD ZONE

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var source = e.Data.Get("Context");
        var target = AssociatedObject?.DataContext;

        if (source == null || target == null || source == target)
        {
            e.DragEffects = DragDropEffects.None;
            ClearVisuals();
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        
        UpdateInsertState(e.GetPosition(AssociatedObject));
        UpdateVisuals();
    }

    private void OnDragLeave(object? sender, RoutedEventArgs e)
    {
        ClearVisuals();
        _insertAfterState = null;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        ClearVisuals();
        
        var source = e.Data.Get("Context");
        var target = AssociatedObject?.DataContext;

        if (source == null || target == null || source == target) return;
        
        bool insertAfter = _insertAfterState ?? (e.GetPosition(AssociatedObject).Y > AssociatedObject!.Bounds.Height / 2);

        if (source is CollectionItemViewModel sCol && target is CollectionItemViewModel tCol)
        {
            MoveCollection(sCol, tCol, insertAfter);
        }
        else if (source is RequestItemViewModel sReq && target is RequestItemViewModel tReq)
        {
            MoveRequest(sReq, tReq, insertAfter);
        }
        else if (source is RequestItemViewModel req && target is CollectionItemViewModel col)
        {
            MoveRequestToCollection(req, col);
        }
        
        _insertAfterState = null;
    }

    // Calculation Helpers

    private void UpdateInsertState(Point p)
    {
        if (AssociatedObject == null) return;

        double height = AssociatedObject.Bounds.Height;
        if (height <= 0) return;

        double yRel = p.Y / height; // Получаем значение от 0.0 до 1.0

        // DEAD ZONE: 40% - 60%
        // If cursor above 40% -> definitely top
        if (yRel < 0.4) 
        {
            _insertAfterState = false;
        }
        else if (yRel > 0.6) 
        {
            _insertAfterState = true;
        }
        else if (_insertAfterState == null)
        {
            _insertAfterState = yRel > 0.5;
        }
    }

    private void UpdateVisuals()
    {
        if (AssociatedObject == null || _insertAfterState == null) return;

        bool bottom = _insertAfterState.Value;
        
        AssociatedObject.Classes.Remove("DragTop");
        AssociatedObject.Classes.Remove("DragBottom");
        
        AssociatedObject.Classes.Add(bottom ? "DragBottom" : "DragTop");
    }

    private void ClearVisuals()
    {
        if (AssociatedObject == null) return;
        AssociatedObject.Classes.Remove("DragTop");
        AssociatedObject.Classes.Remove("DragBottom");
    }

    // Move Logic

    private void MoveCollection(CollectionItemViewModel source, CollectionItemViewModel target, bool insertAfter)
    {
        var list = source.Parent.Collections;
        var oldIndex = list.IndexOf(source);
        var newIndex = list.IndexOf(target);

        if (insertAfter) newIndex++;
        if (oldIndex < newIndex) newIndex--; 
        
        if (newIndex < 0) newIndex = 0;
        if (newIndex >= list.Count) newIndex = list.Count - 1;

        if (oldIndex != newIndex)
        {
            list.Move(oldIndex, newIndex);
            _ = source.Parent.SaveAllAsync();
        }
    }

    private void MoveRequest(RequestItemViewModel source, RequestItemViewModel target, bool insertAfter)
    {
        if (source.Parent == target.Parent)
        {
            var list = source.Parent.Requests;
            var oldIndex = list.IndexOf(source);
            var newIndex = list.IndexOf(target);

            if (insertAfter) newIndex++;

            if (oldIndex < newIndex) newIndex--;

            if (newIndex < 0) newIndex = 0;
            if (newIndex >= list.Count) newIndex = list.Count - 1;

            if (oldIndex != newIndex)
            {
                list.Move(oldIndex, newIndex);

                _ = source.Parent.Parent.SaveCollectionCommand.ExecuteAsync(source.Parent);
            }
        }

        else
        {

            var oldParent = source.Parent;

            oldParent.Requests.Remove(source);

  
            source.Parent = target.Parent;


            var targetList = target.Parent.Requests;
            var targetIndex = targetList.IndexOf(target);
            
            if (insertAfter) targetIndex++;
            
            if (targetIndex < 0) targetIndex = 0;
            if (targetIndex > targetList.Count) targetIndex = targetList.Count;

            targetList.Insert(targetIndex, source);
            
            _ = oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent);
            _ = source.Parent.Parent.SaveCollectionCommand.ExecuteAsync(source.Parent);
        }
    }

    private void MoveRequestToCollection(RequestItemViewModel request, CollectionItemViewModel targetCollection)
    {
        if (request.Parent != targetCollection)
        {
            var oldParent = request.Parent;
            
            oldParent.Requests.Remove(request);
            
            request.Parent = targetCollection;
            
            targetCollection.Requests.Add(request);
            targetCollection.IsExpanded = true;
            
            _ = oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent);
            _ = targetCollection.Parent.SaveCollectionCommand.ExecuteAsync(targetCollection);
        }
    }
}