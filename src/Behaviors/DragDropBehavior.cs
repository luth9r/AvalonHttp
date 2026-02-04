using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using AvalonHttp.ViewModels.CollectionAggregate;

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

    // ========================================
    // Source Logic
    // ========================================
    
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (properties.IsLeftButtonPressed)
        {
            if (e.Source is Control c && (c is TextBox || c is Button))
            {
                return;
            }

            _startPoint = e.GetPosition(AssociatedObject);
            _isDragStart = true;
        }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isDragStart = false;
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDragStart || AssociatedObject == null)
        {
            return;
        }

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
        if (context == null)
        {
            return;
        }

        var dragData = new DataObject();
        dragData.Set("Context", context);

        if (AssociatedObject != null)
        {
            AssociatedObject.Opacity = 0.5;
        }

        try
        {
            await DragDrop.DoDragDrop(e, dragData, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Drag operation failed: {ex.Message}");
        }
        finally
        {
            if (AssociatedObject != null)
            {
                AssociatedObject.Opacity = 1.0;
            }

            _insertAfterState = null;
        }
    }

    // ========================================
    // Target Logic & Visuals
    // ========================================
    
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

        if (!IsValidDropTarget(source, target))
        {
            e.DragEffects = DragDropEffects.None;
            ClearVisuals();
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        UpdateInsertState(e.GetPosition(AssociatedObject));
        UpdateVisuals();
    }

    private bool IsValidDropTarget(object source, object target)
    {
        if (source is CollectionItemViewModel && target is CollectionItemViewModel)
        {
            return true;
        }

        if (source is RequestItemViewModel && target is RequestItemViewModel)
        {
            return true;
        }

        if (source is RequestItemViewModel && target is CollectionItemViewModel)
        {
            return true;
        }

        return false;
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

        if (source == null || target == null || source == target)
        {
            return;
        }

        bool insertAfter = _insertAfterState ?? (e.GetPosition(AssociatedObject).Y > AssociatedObject!.Bounds.Height / 2);

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Drop failed: {ex.Message}");
            }
        });
        
        _insertAfterState = null;
    }

    // ========================================
    // Visual Feedback
    // ========================================
    
    private void UpdateInsertState(Point p)
    {
        if (AssociatedObject == null)
        {
            return;
        }

        double height = AssociatedObject.Bounds.Height;
        if (height <= 0)
        {
            return;
        }

        double yRel = p.Y / height;

        // Dead zone: 40% - 60%
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
        if (AssociatedObject == null || _insertAfterState == null)
        {
            return;
        }

        bool bottom = _insertAfterState.Value;

        AssociatedObject.Classes.Remove("DragTop");
        AssociatedObject.Classes.Remove("DragBottom");
        AssociatedObject.Classes.Add(bottom ? "DragBottom" : "DragTop");
    }

    private void ClearVisuals()
    {
        if (AssociatedObject == null)
        {
            return;
        }

        AssociatedObject.Classes.Remove("DragTop");
        AssociatedObject.Classes.Remove("DragBottom");
    }

    // ========================================
    // Move Operations
    // ========================================
    
    private void MoveCollection(CollectionItemViewModel source, CollectionItemViewModel target, bool insertAfter)
    {
        var parent = source.Parent;

        parent.MoveCollection(source, target, insertAfter);
        
    }

    private void MoveRequest(RequestItemViewModel source, RequestItemViewModel target, bool insertAfter)
    {
        if (source.Parent == target.Parent)
        {
            var collection = source.Parent;
            collection.MoveRequest(source, target, insertAfter);
            _ = Task.Run(async () => await collection.Parent.SaveCollectionCommand.ExecuteAsync(collection));
        }
        else
        {
            MoveRequestBetweenCollections(source, target, insertAfter);
        }
    }

    private void MoveRequestBetweenCollections(RequestItemViewModel source, RequestItemViewModel target, bool insertAfter)
    {
        var oldParent = source.Parent;
        var newParent = target.Parent;
        
        source.SyncToModel();

        oldParent.RemoveRequestFromSource(source);

        var movedVm = new RequestItemViewModel(source.Request, newParent);

        newParent.InsertRequest(movedVm, target, insertAfter);

        newParent.IsExpanded = true;
        newParent.Parent.SelectRequest(movedVm);
        source.Dispose();

        _ = Task.Run(async () => {
            await Task.WhenAll(
                oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent),
                newParent.Parent.SaveCollectionCommand.ExecuteAsync(newParent)
            );
        });
    }

    private void MoveRequestToCollection(RequestItemViewModel request, CollectionItemViewModel targetCollection)
    {
        if (request.Parent == targetCollection)
        {
            return;
        }

        var oldParent = request.Parent;
        
        request.SyncToModel();
        
        oldParent.RemoveRequestFromSource(request);

        var movedVm = new RequestItemViewModel(request.Request, targetCollection);

        targetCollection.InsertRequest(movedVm, null, true);

        targetCollection.IsExpanded = true;
        targetCollection.Parent.SelectRequest(movedVm);
        request.Dispose();

        _ = Task.Run(async () => {
            await Task.WhenAll(
                oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent),
                targetCollection.Parent.SaveCollectionCommand.ExecuteAsync(targetCollection)
            );
        });
    }
}
