using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using AvalonHttp.Messages;
using AvalonHttp.ViewModels.CollectionAggregate;
using CommunityToolkit.Mvvm.Messaging;

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

    // Source Logic
    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var properties = e.GetCurrentPoint(AssociatedObject).Properties;
        if (properties.IsLeftButtonPressed)
        {
            // Don't start drag from interactive elements
            if (e.Source is Control c && (c is TextBox || c is Button))
                return;
                
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
            return;

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
            return;

        var dragData = new DataObject();
        dragData.Set("Context", context);

        if (AssociatedObject != null)
            AssociatedObject.Opacity = 0.5;

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
                AssociatedObject.Opacity = 1.0;
            _insertAfterState = null;
        }
    }

    // Target Logic with Dead Zone
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

        // Validate drop target compatibility
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
        // Collection -> Collection
        if (source is CollectionItemViewModel && target is CollectionItemViewModel)
            return true;

        // Request -> Request (same or different collection)
        if (source is RequestItemViewModel && target is RequestItemViewModel)
            return true;

        // Request -> Collection
        if (source is RequestItemViewModel && target is CollectionItemViewModel)
            return true;

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
            return;

        bool insertAfter = _insertAfterState ?? 
            (e.GetPosition(AssociatedObject).Y > AssociatedObject!.Bounds.Height / 2);

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
            System.Diagnostics.Debug.WriteLine($"Drop operation failed: {ex.Message}");
            
            // WeakReferenceMessenger.Default.Send(new ErrorMessage(
            //     "Failed to Move Item",
            //     $"An error occurred: {ex.Message}"
            // ));
        }
        finally
        {
            _insertAfterState = null;
        }
    }

    // Visual Feedback
    private void UpdateInsertState(Point p)
    {
        if (AssociatedObject == null)
            return;

        double height = AssociatedObject.Bounds.Height;
        if (height <= 0)
            return;

        double yRel = p.Y / height; // 0.0 to 1.0

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
            return;

        bool bottom = _insertAfterState.Value;

        AssociatedObject.Classes.Remove("DragTop");
        AssociatedObject.Classes.Remove("DragBottom");
        AssociatedObject.Classes.Add(bottom ? "DragBottom" : "DragTop");
    }

    private void ClearVisuals()
    {
        if (AssociatedObject == null)
            return;
            
        AssociatedObject.Classes.Remove("DragTop");
        AssociatedObject.Classes.Remove("DragBottom");
    }

    // Move Operations
    private void MoveCollection(CollectionItemViewModel source, CollectionItemViewModel target, bool insertAfter)
    {
        var list = source.Parent.Collections;
        var oldIndex = list.IndexOf(source);
        var newIndex = list.IndexOf(target);

        if (oldIndex == -1 || newIndex == -1)
            return;

        if (insertAfter)
            newIndex++;

        if (oldIndex < newIndex)
            newIndex--;

        newIndex = Math.Clamp(newIndex, 0, list.Count - 1);

        if (oldIndex != newIndex)
        {
            list.Move(oldIndex, newIndex);

            // Save asynchronously with error handling
            _ = Task.Run(async () =>
            {
                try
                {
                    await source.Parent.SaveAllAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save after collection move: {ex.Message}");
                }
            });
        }
    }

    private void MoveRequest(RequestItemViewModel source, RequestItemViewModel target, bool insertAfter)
    {
        if (source.Parent == target.Parent)
        {
            // Move within same collection
            MoveRequestWithinCollection(source, target, insertAfter);
        }
        else
        {
            // Move between collections
            MoveRequestBetweenCollections(source, target, insertAfter);
        }
    }

    private void MoveRequestWithinCollection(RequestItemViewModel source, RequestItemViewModel target, bool insertAfter)
    {
        var list = source.Parent.Requests;
        var oldIndex = list.IndexOf(source);
        var newIndex = list.IndexOf(target);

        if (oldIndex == -1 || newIndex == -1)
            return;

        if (insertAfter)
            newIndex++;

        if (oldIndex < newIndex)
            newIndex--;

        newIndex = Math.Clamp(newIndex, 0, list.Count - 1);

        if (oldIndex != newIndex)
        {
            list.Move(oldIndex, newIndex);

            // Save asynchronously
            _ = Task.Run(async () =>
            {
                try
                {
                    await source.Parent.Parent.SaveCollectionCommand.ExecuteAsync(source.Parent);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save after request move: {ex.Message}");
                }
            });
        }
    }

    private void MoveRequestBetweenCollections(RequestItemViewModel source, RequestItemViewModel target, bool insertAfter)
    {
        var oldParent = source.Parent;
        var newParent = target.Parent;

        // Remove from old collection
        oldParent.Requests.Remove(source);

        // Create new RequestItemViewModel with new parent
        var movedRequest = new RequestItemViewModel(source.ToModel(), newParent);

        // Calculate insert position
        var targetList = newParent.Requests;
        var targetIndex = targetList.IndexOf(target);

        if (insertAfter)
            targetIndex++;

        targetIndex = Math.Clamp(targetIndex, 0, targetList.Count);

        // Insert at position
        targetList.Insert(targetIndex, movedRequest);

        // Select the moved request
        newParent.Parent.SelectRequest(movedRequest);

        // Save both collections asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(
                    oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent),
                    newParent.Parent.SaveCollectionCommand.ExecuteAsync(newParent)
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save after request move between collections: {ex.Message}");
            }
        });
    }

    private void MoveRequestToCollection(RequestItemViewModel request, CollectionItemViewModel targetCollection)
    {
        if (request.Parent == targetCollection)
            return;

        var oldParent = request.Parent;

        // Remove from old collection
        oldParent.Requests.Remove(request);

        // Create new RequestItemViewModel with new parent
        var movedRequest = new RequestItemViewModel(request.ToModel(), targetCollection);

        // Add to new collection
        targetCollection.Requests.Add(movedRequest);
        targetCollection.IsExpanded = true;

        // Select the moved request
        targetCollection.Parent.SelectRequest(movedRequest);

        // Save both collections asynchronously
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.WhenAll(
                    oldParent.Parent.SaveCollectionCommand.ExecuteAsync(oldParent),
                    targetCollection.Parent.SaveCollectionCommand.ExecuteAsync(targetCollection)
                );
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save after request move to collection: {ex.Message}");
            }
        });
    }
}
