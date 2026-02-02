using System.Collections.ObjectModel;
using AvalonHttp.Messages;
using AvalonHttp.Models;
using AvalonHttp.Models.CollectionAggregate;
using AvalonHttp.Services.Interfaces;
using AvalonHttp.ViewModels.CollectionAggregate;
using CommunityToolkit.Mvvm.Messaging;
using FluentAssertions;
using Moq;
using ApiRequest = AvalonHttp.Models.CollectionAggregate.ApiRequest; // Fix ambiguity

namespace AvalonHttpTests.ViewModels.CollectionAggregate;

public class RequestItemViewModelTests : IDisposable
{
    private readonly Mock<ICollectionRepository> _mockRepository;
    private readonly Mock<ISessionService> _mockSession;
    private readonly CollectionsViewModel _parentViewModel;
    private readonly ApiCollection _collection;
    private readonly CollectionItemViewModel _collectionVm;
    private readonly ApiRequest _request;
    private readonly RequestItemViewModel _sut;

    public RequestItemViewModelTests()
    {
        WeakReferenceMessenger.Default.Reset();

        _mockRepository = new Mock<ICollectionRepository>();
        _mockSession = new Mock<ISessionService>();
        
        _parentViewModel = new CollectionsViewModel(_mockRepository.Object, _mockSession.Object);
        
        _collection = new ApiCollection { Id = Guid.NewGuid(), Name = "Col" };
        _collectionVm = new CollectionItemViewModel(_collection, _parentViewModel);
        _parentViewModel.Collections.Add(_collectionVm);
        
        _request = new ApiRequest { Id = Guid.NewGuid(), Name = "Req" };
        _collection.Requests.Add(_request);
        
        _sut = new RequestItemViewModel(_request, _collectionVm);
        _collectionVm.Requests.Add(_sut);
    }

    public void Dispose()
    {
        WeakReferenceMessenger.Default.Reset();
    }

    [Fact]
    public void CreateDeepCopy_CopiesAllProperties_AndCreatesNewId()
    {
        // Arrange
        _sut.Name = "Original";
        _sut.Url = "http://test";
        _request.Headers.Add(new KeyValueItemModel { Key = "K", Value = "V" });

        // Act
        var copy = _sut.CreateDeepCopy();

        // Assert
        copy.Id.Should().NotBe(_request.Id);
        copy.Name.Should().Be("Original");
        copy.Url.Should().Be("http://test");
        copy.Headers.Should().HaveCount(1);
        copy.Headers[0].Key.Should().Be("K");
        copy.Headers[0].Should().NotBeSameAs(_request.Headers[0]); // Verify deep copy
    }

    [Fact]
    public void Delete_SendsConfirmMessage_AndRemovesRequest_OnConfirm()
    {
        // Arrange
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Returns(Task.CompletedTask);
            
        bool messageReceived = false;
        WeakReferenceMessenger.Default.Register<ConfirmMessage>(this, async (r, m) =>
        {
            messageReceived = true;
            await m.OnConfirm();
        });

        // Act
        _sut.DeleteCommand.Execute(null);

        // Assert
        messageReceived.Should().BeTrue();
        _collectionVm.Requests.Should().NotContain(_sut);
        _collection.Requests.Should().NotContain(_request);
    }

    [Fact]
    public async Task MoveToCollection_MovesRequest_AndSavesBothCollections()
    {
        // Arrange
        var targetModel = new ApiCollection { Id = Guid.NewGuid(), Name = "Target" };
        var targetVm = new CollectionItemViewModel(targetModel, _parentViewModel);
        _parentViewModel.Collections.Add(targetVm);
        
        _mockRepository.Setup(r => r.SaveAsync(It.IsAny<ApiCollection>()))
            .Returns(Task.CompletedTask);

        // Act
        await _sut.MoveToCollectionCommand.ExecuteAsync(targetVm);

        // Assert
        // Old collection should be empty
        _collectionVm.Requests.Should().NotContain(_sut);
        _collection.Requests.Should().NotContain(_request);
        
        // New collection should have request
        targetVm.Requests.Should().HaveCount(1);
        targetModel.Requests.Should().HaveCount(1);
        
        // Verify save was called for BOTH
        _mockRepository.Verify(r => r.SaveAsync(_collection), Times.Once);
        _mockRepository.Verify(r => r.SaveAsync(targetModel), Times.Once);
    }
}
