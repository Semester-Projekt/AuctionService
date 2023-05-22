using Microsoft.AspNetCore.Mvc;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Model;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.Net.Http.Json;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Controllers;

[ApiController]
[Route("[controller]")]
public class AuctionController : ControllerBase
{
    private readonly ILogger<AuctionController> _logger;
    private readonly IConfiguration _config;
    private AuctionRepository _auctionRepository;

    public AuctionController(ILogger<AuctionController> logger, IConfiguration config, AuctionRepository userRepository)
    {
        _config = config;
        _logger = logger;
        _auctionRepository = userRepository;


        //Logger host information
        var hostName = System.Net.Dns.GetHostName();
        var ips = System.Net.Dns.GetHostAddresses(hostName);
        var _ipaddr = ips.First().MapToIPv4().ToString();
        _logger.LogInformation(1, $"Auth service responding from {_ipaddr}");

    }


    



    //GET
    [Authorize]
    [HttpGet("getallauctions")]
    public async Task<IActionResult> GetAllAuctions()
    {
        _logger.LogInformation("GetAllAuctions function hit");

        var auctions = _auctionRepository.GetAllAuctions().Result;

        _logger.LogInformation("Total auctions: " + auctions.Count());

        if (auctions == null)
        {
            return BadRequest("Auction list is empty");
        }

        var filteredAuctions = auctions.Select(c => new
        {
            c.ArtifactID,
            c.AuctionEndDate,
            c.CurrentBid,
            c.FinalBid
        });

        return Ok(filteredAuctions);
    }

    [Authorize]
    [HttpGet("getAuctionById/{auctionId}")]
    public async Task<IActionResult> GetAuctionById(int auctionId)
    {
        _logger.LogInformation("GetAuctionById function hit");

        var auction = _auctionRepository.GetAuctionById(auctionId).Result;

        if (auction == null)
        {
            return BadRequest("No auction found");
        }

        var result = new
        {
            ArtifactID = auction.ArtifactID,
            AuctionEndDate = auction.AuctionEndDate,
            CurrentBid = auction.CurrentBid,
            FinalBid = auction.FinalBid,
            BidHistory = auction.BidHistory
        };


        return Ok(result);
    }

    [Authorize]
    [HttpGet("getartifactid/{id}")]
    public async Task<IActionResult> GetArtifactIdFromArtifactService(int id)
    {
        _logger.LogInformation("GetArtifactIdFromArtifactService function hit");

        using (HttpClient client = new HttpClient())
        {
            string artifactServiceUrl = "http://localhost:5235";
            string getArtifactEndpoint = "/catalogue/getArtifactById/" + id;

            _logger.LogInformation(artifactServiceUrl + getArtifactEndpoint);

            HttpResponseMessage response = await client.GetAsync(artifactServiceUrl + getArtifactEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Failed to retrieve ArtifactID from ArtifactService");
            }

            // Deserialize the JSON response into an Artifact object
            ArtifactDTO artifact = await response.Content.ReadFromJsonAsync<ArtifactDTO>();

            // Extract the ArtifactID from the deserialized Artifact object
            int artifactId = artifact.ArtifactID;


            var filteredArtifact = new
            {
                artifact.ArtifactName,
                artifact.ArtifactDescription,
                artifact.ArtifactPicture
            };

            return Ok(filteredArtifact);
        }
    }

    [Authorize]
    [HttpGet("getAuctionsByCategoryCode/{categoryCode}"), DisableRequestSizeLimit]
    public async Task<IActionResult> GetAuctionsByCategoryCode(string categoryCode)
    {
        _logger.LogInformation("getAuctionsByCategoryCode function hit");




        return Ok();
    }






    //POST
    [Authorize]
    [HttpPost("addauction/{id}")]
    public async Task<IActionResult> AddAuctionFromArtifactId(int id)
    {
        _logger.LogInformation("AddAuctionFromArtifactId function hit");

        using (HttpClient client = new HttpClient())
        {
            string artifactServiceUrl = "http://localhost:5235";
            string getArtifactEndpoint = "/catalogue/getArtifactById/" + id;

            HttpResponseMessage response = await client.GetAsync(artifactServiceUrl + getArtifactEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Failed to retrieve ArtifactID from ArtifactService");
            }


            // Deserialize the JSON response into an Artifact object
            ArtifactDTO artifact = await response.Content.ReadFromJsonAsync<ArtifactDTO>();
            
            // Extract the ArtifactID from the deserialized Artifact object
            int artifactId = artifact.ArtifactID;

            _logger.LogInformation("ArtifactID: " + artifactId);

            int latestID = _auctionRepository.GetNextAuctionId(); // Gets latest ID in _artifacts + 1

            // GetArtifactById til at hente det ArtifactID man vil sende til newAuction


            // Create a new instance of Auction and set its properties
            var newAuction = new Auction
            {
                AuctionId = latestID,
                ArtifactID = id
            };

            // Add the new auction to the repository or perform necessary operations
            _auctionRepository.AddNewAuction(newAuction);
            _logger.LogInformation("New Auction object added");

            var result = new
            {
                AuctionEndDate = newAuction.AuctionEndDate,
                ArtifactID = newAuction.ArtifactID
            };

            return Ok(result);
        }
    }






    //PUT
    [Authorize]
    [HttpPut("updateAuction/{auctionId}"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdateAuction(int auctionId, [FromBody] Auction? auction)
    {
        _logger.LogInformation("UpdateAuction function hit");

        var updatedAuction = await _auctionRepository.GetAuctionById(auctionId);

        if (updatedAuction == null)
        {
            return BadRequest("Auction does not exist");
        }
        _logger.LogInformation("Auction for update: " + updatedAuction.AuctionId);

        await _auctionRepository.UpdateAuction(auctionId, auction!);

        var newUpdatedArtifact = await _auctionRepository.GetAuctionById(auctionId);

        return Ok($"Artifact, {updatedAuction.AuctionId}, has been updated. New AuctionEndDate: {newUpdatedArtifact.AuctionEndDate}");
    }






    //DELETE
    [Authorize]
    [HttpDelete("deleteAuction/{auctionId}"), DisableRequestSizeLimit]
    public async Task<IActionResult> DeleteAuction(int auctionId)
    {
        _logger.LogInformation("DeleteAuction function hit");

        var deletedAuction = await _auctionRepository.GetAuctionById(auctionId);

        if (deletedAuction == null)
        {
            return BadRequest("No auction with id: " + auctionId);
        }
        else if (deletedAuction.CurrentBid != null && deletedAuction.FinalBid == null)
        {
            return BadRequest("Cannot delete auction with active bids");
        }
        else await _auctionRepository.DeleteAuction(auctionId);
        _logger.LogInformation($"Auction with id: {auctionId} deleted");

        return Ok($"Auction has been deleted");
    }



}