using System.Collections.ObjectModel;
using AvalonHttp.Models;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;
using AvalonHttp.ViewModels.CollectionAggregate;
using AvalonHttp.Messages;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;

namespace AvalonHttpTests.ViewModels.CollectionAggregate;

public class CollectionsViewModelTests : IDisposable
{
    private readonly Mock<ICollectionRepository> _mockRepository;
    private readonly Mock<ISessionService> _mockSession;
    private readonly CollectionsViewModel _sut; // System Under Test

    public CollectionsViewModelTests()
    {
        _mockRepository = new Mock<ICollectionRepository>();
        _mockSession = new Mock<ISessionService>();
        _sut = new CollectionsViewModel(_mockRepository.Object, _mockSession.Object);
        WeakReferenceMessenger.Default.Reset();
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public void Constructor_InitializesProperties_WithDefaultValues()
    {
        _sut.Collections.Should().BeEmpty();
        _sut.SelectedRequest.Should().BeNull();
        _sut.IsLoading.Should().BeFalse();
        _sut.HasCollections.Should().BeFalse();
        _sut.HasSelection.Should().BeFalse();
    }

    [Fact]
    public async Task InitializeAsync_LoadsCollections_Successfully()
    {
        // Arrange
        var collections = new List<ApiCollection>
        {
            new ApiCollection { Id = Guid.NewGuid(), Name = "Test Collection 1" },
            new ApiCollection { Id = Guid.NewGuid(), Name = "Test Collection 2" }
        };
        
        _mockRepository
            .Setup(r => r.LoadAllAsync())
            .ReturnsAsync(collections);
        
        _mockSession
            .Setup(s => s.LoadStateAsync())
            .ReturnsAsync(new AppState());

        // Act
        await _sut.InitializeAsync();

        // Assert
        _sut.Collections.Should().HaveCount(2);
        _sut.Collections[0].Name.Should().Be("Test Collection 1");
        _mockRepository.Verify(r => r.LoadAllAsync(), Times.Once);
    }
    
    [Fact]
    public async Task InitializeAsync_SendsErrorMessage_OnFailure()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.LoadAllAsync())
            .ThrowsAsync(new Exception("Test failure"));
            
        bool errorReceived = false;
        WeakReferenceMessenger.Default.Register<ErrorMessage>(this, (r, m) =>
        {
            errorReceived = true;
            m.Title.Should().Be("Failed to Load Collections");
        });

        // Act
        await _sut.InitializeAsync();

        // Assert
        errorReceived.Should().BeTrue();
    }
    
    [Fact]
    public async Task CreateCollection_AddsNewCollection_ToList()
    {
        // Arrange
        _mockRepository
            .Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.CreateCollectionCommand.ExecuteAsync(null);

        // Assert
        _sut.Collections.Should().HaveCount(1);
        _sut.Collections[0].Name.Should().StartWith("New Collection");
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<ApiCollection>()), Times.Once);
    }
    
    [Fact]
    public void DeleteCollection_SendsConfirmMessage_AndRemovesCollection_OnConfirm()
    {
        // Arrange
        var collection = new ApiCollection { Id = Guid.NewGuid(), Name = "To Delete" };
        var collectionVm = new CollectionItemViewModel(collection, _sut);
        _sut.Collections.Add(collectionVm);

        _mockRepository
            .Setup(r => r.DeleteAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        bool messageReceived = false;
        
        // Register a recipient to intercept the confirmation message
        WeakReferenceMessenger.Default.Register<ConfirmMessage>(this, async (r, m) =>
        {
            messageReceived = true;
            // Execute the confirmation action acting as the user clicking "Yes"
            await m.OnConfirm(); 
        });

        // Act
        _sut.DeleteCollectionCommand.Execute(collectionVm);

        // Assert
        messageReceived.Should().BeTrue("ConfirmMessage should be sent");
        _sut.Collections.Should().BeEmpty("Collection should be removed after confirmation");
        _mockRepository.Verify(r => r.DeleteAsync(collection.Id), Times.Once);
    }

    [Fact]
    public async Task DuplicateCollection_AddsCopy_ToList()
    {
        // Arrange
        var originalRequests = new ObservableCollection<ApiRequest>
        {
            new ApiRequest { Id = Guid.NewGuid(), Name = "Req 1" }
        };
        var originalCollection = new ApiCollection 
        { 
            Id = Guid.NewGuid(), 
            Name = "Original", 
            Description = "Desc",
            Requests = originalRequests
        };
        var originalVm = new CollectionItemViewModel(originalCollection, _sut);
        
        ApiCollection savedCollection = null;
        _mockRepository
            .Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Callback<ApiCollection>(c => savedCollection = c)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DuplicateCollectionCommand.ExecuteAsync(originalVm);

        // Assert
        _sut.Collections.Should().HaveCount(1); // Since we didn't add the original to the list, only the new one is there?
        // Wait, duplicated collection is added to the list.
         
        // Let's ensure the list contains the new one.
        var newVm = _sut.Collections.First();
        newVm.Name.Should().StartWith("Original (Copy)");
        newVm.Description.Should().Be("Desc");
        newVm.Requests.Should().HaveCount(1);
        newVm.Requests[0].Name.Should().Be("Req 1");
        newVm.Requests[0].Id.Should().NotBe(originalRequests[0].Id); // IDs should be different
        
        savedCollection.Should().NotBeNull();
        savedCollection.Id.Should().NotBe(originalCollection.Id);
    }
    
    [Fact]
    public async Task SaveCollection_CallsRepository_WithCorrectData()
    {
        // Arrange
        var collection = new ApiCollection { Id = Guid.NewGuid(), Name = "Original" };
        var collectionVm = new CollectionItemViewModel(collection, _sut);
        collectionVm.Name = "Updated Name";

        ApiCollection savedCollection = null;
        _mockRepository
            .Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Callback<ApiCollection>(c => savedCollection = c)
            .Returns(Task.CompletedTask);

        // Act
        await _sut.SaveCollectionCommand.ExecuteAsync(collectionVm);

        // Assert
        savedCollection.Should().NotBeNull();
        savedCollection.Name.Should().Be("Updated Name");
        savedCollection.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(1));
    }
    
    [Fact]
    public void SelectRequest_UpdatesSelectedRequest_AndRaisesEvent()
    {
        // Arrange
        var collection = new ApiCollection 
        { 
            Id = Guid.NewGuid(), 
            Requests = new ObservableCollection<ApiRequest>
            {
                new ApiRequest { Id = Guid.NewGuid(), Name = "Test Request" }
            }
        };
        var collectionVm = new CollectionItemViewModel(collection, _sut);
        var requestVm = collectionVm.Requests[0];

        ApiRequest selectedRequest = null;
        _sut.RequestSelected += (sender, request) => selectedRequest = request;

        // Act
        _sut.SelectRequest(requestVm);

        // Assert
        _sut.SelectedRequest.Should().Be(requestVm);
        requestVm.IsSelected.Should().BeTrue();
        selectedRequest.Should().Be(requestVm.Request);
    }
    
    
}