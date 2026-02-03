using System.Collections.ObjectModel;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;
using AvalonHttp.ViewModels.CollectionAggregate;
using FluentAssertions;
using Moq;

namespace AvalonHttpTests.ViewModels.CollectionAggregate;

public class CollectionItemViewModelTests
{
    private readonly Mock<ICollectionRepository> _mockRepository;
    private readonly Mock<ISessionService> _mockSession;
    private readonly CollectionsViewModel _parentViewModel;
    private readonly ApiCollection _collection;
    private readonly CollectionItemViewModel _sut;

    public CollectionItemViewModelTests()
    {
        _mockRepository = new Mock<ICollectionRepository>();
        _mockSession = new Mock<ISessionService>();
        
        _parentViewModel = new CollectionsViewModel(_mockRepository.Object, _mockSession.Object);
        
        _collection = new ApiCollection 
        { 
            Id = Guid.NewGuid(), 
            Name = "Test Collection", 
            Description = "Test Description" 
        };
        
        _sut = new CollectionItemViewModel(_collection, _parentViewModel);
    }

    [Fact]
    public void Constructor_InitializesProperties_FromModel()
    {
        _sut.Name.Should().Be(_collection.Name);
        _sut.Description.Should().Be(_collection.Description);
        _sut.Id.Should().Be(_collection.Id);
    }

    [Fact]
    public async Task FinishRename_UpdatesModel_AndSaves_WhenNameChanged()
    {
        // Arrange
        _sut.StartRenameCommand.Execute(null);
        _sut.Name = "New Name";
        
        var tcs = new TaskCompletionSource<ApiCollection>();
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.FinishRenameCommand.ExecuteAsync(null);

        // Assert
        _sut.Collection.Name.Should().Be("New Name");
        _sut.IsEditing.Should().BeFalse();
        // Since SaveCollectionCommand calls repository save indirectly via parent
        // We verify that the parent's SaveCollectionCommand logic eventually calls the repo
         _mockRepository.Verify(r => r.SaveAsync(It.IsAny<ApiCollection>()), Times.Once);
    }
    
    [Fact]
    public async Task AddRequest_AddsRequestToModelAndViewModel_AndSaves()
    {
        // Arrange
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.AddRequestCommand.ExecuteAsync(null);

        // Assert
        _sut.Requests.Should().HaveCount(1);
        _sut.Collection.Requests.Should().HaveCount(1);
        _sut.Requests[0].Name.Should().StartWith("New Request");
        
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<ApiCollection>()), Times.Once);
    }

    [Fact]
    public async Task DeleteRequest_RemovesRequest_AndSaves()
    {
        // Arrange
        var request = new ApiRequest { Id = Guid.NewGuid(), Name = "Req 1" };
        _collection.Requests.Add(request);
        var requestVm = new RequestItemViewModel(request, _sut);
        _sut.Requests.Add(requestVm);
        
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DeleteRequestCommand.ExecuteAsync(requestVm);

        // Assert
        _sut.Requests.Should().BeEmpty();
        _sut.Collection.Requests.Should().BeEmpty();
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<ApiCollection>()), Times.Once);
    }

    [Fact]
    public async Task DuplicateRequest_CreatesCopy_AndSaves()
    {
        // Arrange
        var request = new ApiRequest { Id = Guid.NewGuid(), Name = "Req 1", MethodString = "POST" };
        _collection.Requests.Add(request);
        var requestVm = new RequestItemViewModel(request, _sut);
        _sut.Requests.Add(requestVm);
        
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.DuplicateRequestCommand.ExecuteAsync(requestVm);

        // Assert
        _sut.Requests.Should().HaveCount(2);
        _sut.Collection.Requests.Should().HaveCount(2);
        
        var newRequest = _sut.Requests[1]; // Should be inserted after
        newRequest.Name.Should().StartWith("Req 1 (Copy)");
        newRequest.Id.Should().NotBe(request.Id);
        newRequest.Method.Should().Be("POST"); // Should verify deep copy of properties
        
        _mockRepository.Verify(r => r.SaveAsync(It.IsAny<ApiCollection>()), Times.Once);
    }

    [Fact]
    public async Task MoveRequestUp_MovesRequest_WhenPossible()
    {
         // Arrange
        var r1 = new ApiRequest { Id = Guid.NewGuid(), Name = "1" };
        var r2 = new ApiRequest { Id = Guid.NewGuid(), Name = "2" };
        
        _collection.Requests.Add(r1);
        _collection.Requests.Add(r2);
        
        var vm1 = new RequestItemViewModel(r1, _sut);
        var vm2 = new RequestItemViewModel(r2, _sut);
        
        _sut.Requests.Add(vm1);
        _sut.Requests.Add(vm2);
        
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.MoveRequestUpCommand.ExecuteAsync(vm2);

        // Assert
        _sut.Requests[0].Should().Be(vm2);
        _sut.Requests[1].Should().Be(vm1);
        _collection.Requests[0].Should().Be(r2);
        _collection.Requests[1].Should().Be(r1);
    }
}
