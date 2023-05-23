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

    //docker test

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
            c.FinalBid,
            c.BidHistory
        });

        return Ok(filteredAuctions);
    }

    [HttpGet("getAuctionById/{auctionId}")]
    public async Task<Auction> GetAuctionById(int auctionId)
    {
        _logger.LogInformation("GetAuctionById function hit");

        var auction = _auctionRepository.GetAuctionById(auctionId).Result;

        

        var bidHistory = _auctionRepository.GetAllBids().Result.Where(b => b.ArtifactId == auction.ArtifactID);
        auction.BidHistory = (List<Bid>?)bidHistory.OrderByDescending(b => b.BidDate).ToList();

        var currentBid = _auctionRepository.GetAllBids().Result.Where(b => b.ArtifactId == auction.ArtifactID).OrderByDescending(b => b.BidAmount).FirstOrDefault().BidAmount;
        auction.CurrentBid = currentBid;

        int? finalBid;
        if (auction.AuctionEndDate < DateTime.Now)
        {
            finalBid = _auctionRepository.GetAllBids().Result.Where(b => b.ArtifactId == auction.ArtifactID).OrderByDescending(b => b.BidAmount).FirstOrDefault().BidAmount;
        }
        else
        {
            finalBid = null;
        }

        var result = new
        {
            ArtifactID = auction.ArtifactID,
            AuctionEndDate = auction.AuctionEndDate,
            CurrentBid = currentBid,
            FinalBid = finalBid,
            BidHistory = auction.BidHistory!.Select(b => new
            {
                BidOwner = new
                {
                    UserName = b.BidOwner!.UserName,
                    UserEmail = b.BidOwner!.UserEmail,
                    UserPhone = b.BidOwner!.UserPhone,
                },
                BidAmount = b.BidAmount,
                BidDate = b.BidDate
            })
        };

        return auction;
        //return Ok(result);
    }
    
    [HttpGet("getartifactid/{id}")]
    public async Task<IActionResult> GetArtifactIdFromArtifactService(int id)
    {
        _logger.LogInformation("GetArtifactIdFromArtifactService function hit");

        using (HttpClient client = new HttpClient())
        {
            string artifactServiceUrl = "http://catalogue:80";
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

    [HttpGet("getUserFromUserService/{id}"), DisableRequestSizeLimit]
    public async Task<ActionResult<UserDTO>> GetUserFromUserService(int id)
    {
        _logger.LogInformation("AuctionService - GetUser function hit");

        using (HttpClient client = new HttpClient())
        {
            string userServiceUrl = "http://user:80";
            string getUserEndpoint = "/user/getUser/" + id;

            _logger.LogInformation(userServiceUrl + getUserEndpoint);

            HttpResponseMessage response = await client.GetAsync(userServiceUrl + getUserEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Failed to retrieve UserId from UserService");
            }

            var userResponse = await response.Content.ReadFromJsonAsync<UserDTO>();

            if (userResponse != null)
            {
                _logger.LogInformation($"MongId: {userResponse.MongoId}");
                _logger.LogInformation($"UserName: {userResponse.UserName}");

                
                return Ok(userResponse);
            }
            else
            {
                return BadRequest("Failed to retrieve User object");
            }
        }
    }
    





    //POST
    [HttpPost("addauction/{artifactID}")]
    public async Task<IActionResult> AddAuctionFromArtifactId(int artifactID)
    {
        _logger.LogInformation("AddAuctionFromArtifactId function hit");

        using (HttpClient client = new HttpClient())
        {
            string artifactServiceUrl = "http://catalogue:80";
            string getArtifactEndpoint = "/catalogue/getArtifactById/" + artifactID;

            HttpResponseMessage response = await client.GetAsync(artifactServiceUrl + getArtifactEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Failed to retrieve ArtifactID from ArtifactService");
            }


            // Deserialize the JSON response into an Artifact object
            ArtifactDTO artifact = response.Content.ReadFromJsonAsync<ArtifactDTO>().Result!;

            int latestID = _auctionRepository.GetNextAuctionId(); // Gets latest ID in _artifacts + 1
            
            // GetArtifactById til at hente det ArtifactID man vil sende til newAuction


            // Create a new instance of Auction and set its properties
            var newAuction = new Auction
            {
                AuctionId = latestID,
                ArtifactID = artifactID
            };

            // Add the new auction to the repository or perform necessary operations
            _auctionRepository.AddNewAuction(newAuction);
            _logger.LogInformation("New Auction object added");

            var result = new
            {
                AuctionEndDate = newAuction.AuctionEndDate,
                ArtifactID = newAuction.ArtifactID
            };

            _logger.LogInformation($"result: {result.ArtifactID} + {result.AuctionEndDate}");

            return Ok(result);
        }
    }

    [HttpPost("addBid/{userId}/{auctionid}")] // DENNE METODE SKAL KØRE IGENNEM RABBIT, HOW???
    public async Task<IActionResult> AddNewBid([FromBody] Bid? bid, int userId, int auctionId)
    {
        _logger.LogInformation("AddNewBid function hit");

        var userResponse = await GetUserFromUserService(userId);
        _logger.LogInformation("userresponse result: " + userResponse.Result);

        if (userResponse.Result is ObjectResult objectResult && objectResult.Value is UserDTO user)
        {
            var latestId = _auctionRepository.GetNextBidId();

            _logger.LogInformation("BidId: " + latestId);

            if (user != null)
            {

                var newBid = new Bid
                {
                    BidId = latestId,
                    ArtifactId = bid!.ArtifactId,
                    BidOwner = user,
                    BidAmount = bid.BidAmount
                };
                _logger.LogInformation("new Bid object made. BidId: " + newBid.BidId);

                _auctionRepository.AddNewBid(newBid);


                var result = new
                {
                    ArtifactId = newBid.ArtifactId,
                    BidOwner = new
                    {
                        user.UserName,
                        user.UserEmail,
                        user.UserPhone
                    },
                    BidAmount = newBid.BidAmount,
                    BidDate = bid.BidDate
                };


                var auction = await GetAuctionById(auctionId);

                await _auctionRepository.UpdateAuctionBid(auctionId, auction, newBid);

                _logger.LogInformation("addNewBid - artifactID: " + auction.ArtifactID);
                _logger.LogInformation("addNewBid - bidAmount på nye bid: " + bid.BidAmount);


                return Ok(result);
            }
            else
            {
                return BadRequest("User object is null");
            }
        }
        else
        {
            return BadRequest("Failed to retrieve User object");
        }
    }






    //PUT
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