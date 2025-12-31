using Moq;
using Artemis.Core.Services;
using Artemis.Core.Interfaces;
using Artemis.Core.Models;

namespace Artemis.Core.Tests;

public class EvaluationServiceTests
{
    private readonly Mock<ICriteriaTreeService> _criteriaService;
    private readonly Mock<ILocationRepository> _locationRepo;
    private readonly Mock<IScoreRepository> _scoreRepo;
    private readonly Mock<IPointOfInterestRepository> _poiRepo;
    private readonly EvaluationService _service;
    
    public EvaluationServiceTests()
    {
        _criteriaService = new Mock<ICriteriaTreeService>();
        _locationRepo = new Mock<ILocationRepository>();
        _scoreRepo = new Mock<IScoreRepository>();
        _poiRepo = new Mock<IPointOfInterestRepository>();
        _service = new EvaluationService(_criteriaService.Object, _locationRepo.Object, _scoreRepo.Object, _poiRepo.Object);

        _poiRepo
            .Setup(x =>
                x.ListByBoundingBoxAndCategoryAsync(
                    It.IsAny<BoundingBox>(),
                    It.IsAny<Category>(),
                    CancellationToken.None)
                )
            .ReturnsAsync([
                new PointOfInterest
                {
                    Id = 1,
                    BatchId = 1,
                    SourceXref = "1",
                    Category = Category.Library,
                    Latitude = 38.898647825153304,
                    Longitude = -77.04055545099781
                }
            ]);
    }
    
    [Fact]
    public async Task ScoreAsync_SingleRootNode_ScoreOfZero()
    {
        var location = new Location();
        var criteria = new GroupNode(1, OperatorType.And);
        var sink = new Dictionary<int, EvaluationResult>();
        var score = await _service.ScoreAsync(location, criteria, sink);
        Assert.Equal(0, score.Score);
    }
    
    [Fact]
    public async Task ScoreAsync_SingleTermAndZeroDistance_ScoreOfOne()
    {
        
        var location = new Location
        {
            Id = 1,
            Latitude = 38.898647825153304,
            Longitude = -77.04055545099781
        };
        var criteria = new GroupNode(1, OperatorType.And);
        criteria.Children.Add(new TermNode(2, Category.Library, 10));
        var sink = new Dictionary<int, EvaluationResult>();
        var score = await _service.ScoreAsync(location, criteria, sink);
        Assert.Equal(1, score.Score);
    }
}
