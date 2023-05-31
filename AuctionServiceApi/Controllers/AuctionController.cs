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
using System.Text.Json;
using RabbitMQ.Client;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Net;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Diagnostics;
using NLog;
using MongoDB.Driver.Core.Bindings;
using RabbitMQ.Client.Events;
using System.Net.Http.Headers;
using System.Net.Http;

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
        _logger.LogInformation($"Connecting to rabbitMQ on {_config["rabbithostname"]}"); //tester om den kommer på rigtig rabbitserver. Skrives i logs.


        //Logger host information
        var hostName = System.Net.Dns.GetHostName();
        var ips = System.Net.Dns.GetHostAddresses(hostName);
        var _ipaddr = ips.First().MapToIPv4().ToString();
        _logger.LogInformation(1, $"Auth service responding from {_ipaddr}");

    }

    //RabbitMQ start
    //  private object PublishNewArtifactMessage(Artifact newArtifact, object result)
    private object PublishNewBidMessage(object result)
    {
        // Configure RabbitMQ connection settings
        var factory = new ConnectionFactory()
        {
            HostName = _config["rabbithostname"], // Replace with your RabbitMQ server hostname
            UserName = "worker",     // Replace with your RabbitMQ username
            Password = "1234"      // Replace with your RabbitMQ password
        };

        using (var connection = factory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            // Declare a queue. Sender new-bid-queue
            channel.QueueDeclare(queue: "new-bid-queue",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            //Receiver bid-data-queue
            channel.QueueDeclare(queue: "bid-data-queue",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);

            var consumer = new EventingBasicConsumer(channel);
            consumer.Received += (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                Console.WriteLine($" [x] Received {message}");

                // Parse the received message as JSON
                var jsonDocument = JsonDocument.Parse(message);
            };

            // Return the result object
            return result;
        }
    }
    //RabbitMQ slut


    
    // VERSION_ENDEPUNKT
    [HttpGet("version")]
    public async Task<Dictionary<string, string>> GetVersion()
    {
        var properties = new Dictionary<string, string>();
        var assembly = typeof(Program).Assembly;

        properties.Add("service", "Auction");
        var ver = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion;
        properties.Add("version", ver!);

        try
        {
            var hostName = System.Net.Dns.GetHostName();
            var ips = await System.Net.Dns.GetHostAddressesAsync(hostName);
            var ipa = ips.First().MapToIPv4().ToString();
            properties.Add("hosted-at-address", ipa);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            properties.Add("hosted-at-address", "Could not resolve IP-address");
        }

        return properties;
    }






    //GET
    [Authorize]
    [HttpGet("getallauctions")]
    public async Task<IActionResult> GetAllAuctions()
    {
        _logger.LogInformation("AuctionService - GetAllAuctions function hit");

        var auctions = _auctionRepository.GetAllAuctions().Result;

        _logger.LogInformation("AuctionService - Total auctions: " + auctions.Count());

        if (auctions == null)
        {
            return BadRequest("AuctionService - Auction list is empty");
        }
        
        var filteredAuctions = auctions.Select(c => new
        {
            c.ArtifactID,
            c.AuctionEndDate,
            c.CurrentBid,
            c.FinalBid,
            c.BidHistory
        });

        return Ok(auctions);
    }

    [Authorize]
    [HttpGet("getAuctionById/{auctionId}")]
    public async Task<Auction> GetAuctionById(int auctionId)
    {
        _logger.LogInformation("AuctionService - GetAuctionById function hit");

        var auction = _auctionRepository.GetAuctionById(auctionId).Result;
        
        var bidHistory = _auctionRepository.GetAllBids().Result.Where(b => b.ArtifactId == auction.ArtifactID);
        auction.BidHistory = (List<Bid>?)bidHistory.OrderByDescending(b => b.BidDate).ToList();
        
        int? finalBid;
        if (auction.AuctionEndDate < DateTime.Now)
        {
            finalBid = _auctionRepository.GetAllBids().Result.Where(b => b.ArtifactId == auction.ArtifactID).OrderByDescending(b => b.BidAmount).FirstOrDefault()!.BidAmount;
        }
        else
        {
            finalBid = null;
        }

        var result = new
        {
            ArtifactID = auction.ArtifactID,
            AuctionEndDate = auction.AuctionEndDate,
            CurrentBid = auction.CurrentBid,
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
    public async Task<IActionResult> GetArtifactIdFromCatalogueService(int id)
    {
        _logger.LogInformation("AuctionService - GetArtifactIdFromCatalogueService function hit");

        using (HttpClient client = new HttpClient())
        {
            //string catalogueServiceUrl = "http://catalogue:80";
            //string catalogueServiceUrl = "http://localhost:4000";
            string catalogueServiceUrl = Environment.GetEnvironmentVariable("CATALOGUE_SERVICE_URL")!;
            string getCatalogueEndpoint = "/catalogue/getArtifactById/" + id;

            _logger.LogInformation(catalogueServiceUrl + getCatalogueEndpoint);

            HttpResponseMessage response = await client.GetAsync(catalogueServiceUrl + getCatalogueEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "AuctionService - Failed to retrieve ArtifactID from CatalogueService");
            }

            // Deserialize the JSON response into an Artifact object
            ArtifactDTO artifact = await response.Content.ReadFromJsonAsync<ArtifactDTO>();

            // Extract the ArtifactID from the deserialized Artifact object
            int artifactId = (int)artifact!.ArtifactID!;
            
            return Ok(artifact);
        }
    }

    [HttpGet("getUserFromUserService/{id}"), DisableRequestSizeLimit]
    public async Task<ActionResult<UserDTO>> GetUserFromUserService(int id)
    {
        _logger.LogInformation("AuctionService - GetUser function hit");

        using (HttpClient _httpClient = new HttpClient())
        {
            string userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")!;
            string getUserEndpoint = "/user/getUser/" + id;

            _logger.LogInformation(userServiceUrl + getUserEndpoint);

            // Retrieve the current user's token from the request
            var tokenValue = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            _logger.LogInformation("CatalogueService - token first default: " + tokenValue);
            var token = tokenValue?.Replace("Bearer ", "");
            _logger.LogInformation("CatalogueService - token w/o bearer: " + token);

            // Create a new HttpRequestMessage to include the token
            var request = new HttpRequestMessage(HttpMethod.Get, userServiceUrl + getUserEndpoint);
            //request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Send the request to the AuctionService API to retrieve all auctions
            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "AuctionService - Failed to retrieve UserId from UserService");
            }

            var userResponse = await response.Content.ReadFromJsonAsync<UserDTO>();

            if (userResponse != null)
            {
                _logger.LogInformation($"AuctionService - MongId: {userResponse.MongoId}");
                _logger.LogInformation($"AuctionService - UserName: {userResponse.UserName}");


                return Ok(userResponse);
            }
            else
            {
                return BadRequest("AuctionService - Failed to retrieve User object");
            }
        }
    }






    //POST
    [HttpPost("addauction/{artifactID}")]
    public async Task<IActionResult> AddAuctionFromArtifactId(int artifactID)
    {
        _logger.LogInformation("AuctionService - AddAuctionFromArtifactId function hit");

        using (HttpClient _httpClient = new HttpClient())
        {
            string catalogueServiceUrl = Environment.GetEnvironmentVariable("CATALOGUE_SERVICE_URL")!;
            string getCatalogueEndpoint = "/catalogue/getArtifactById/" + artifactID;

            _logger.LogInformation($"AuctionService: {catalogueServiceUrl + getCatalogueEndpoint}");

            // Retrieve the current user's token from the request
            var tokenValue = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            _logger.LogInformation("CatalogueService - token first default: " + tokenValue);
            var token = tokenValue?.Replace("Bearer ", "");
            _logger.LogInformation("CatalogueService - token w/o bearer: " + token);

            // Create a new HttpRequestMessage to include the token
            var request = new HttpRequestMessage(HttpMethod.Get, catalogueServiceUrl + getCatalogueEndpoint);
            //request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Send the request to the UserService API to retrieve the user
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "AuctionService - Failed to retrieve ArtifactID from ArtifactService");
            }


            // Deserialize the JSON response into an Artifact object
            ArtifactDTO artifact = response.Content.ReadFromJsonAsync<ArtifactDTO>().Result!;

            int latestID = _auctionRepository.GetNextAuctionId(); // Gets latest ID in _auctions + 1

            // GetArtifactById til at hente det ArtifactID man vil sende til newAuction


            // Create a new instance of Auction and set its properties
            var newAuction = new Auction
            {
                AuctionId = latestID,
                ArtifactID = artifactID
            };

            // Add the new auction to the repository or perform necessary operations

            if (artifact.Status != "Active")
            {
                _auctionRepository.AddNewAuction(newAuction);

                _logger.LogInformation("AuctionService - New Auction object added");

                var result = new
                {
                    AuctionEndDate = newAuction.AuctionEndDate,
                    ArtifactID = newAuction.ArtifactID
                };

                _logger.LogInformation($"result: ArtifactID: {result.ArtifactID} + AuctionEndDate: {result.AuctionEndDate}");


                _logger.LogInformation($"AuctionService - current Artifact.Status: {artifact.Status}");

                string getActivationEndpoint = "/catalogue/activateArtifact/" + artifactID; // Call activateArtifact endpoint to change Artifact status to 'Active'

                _logger.LogInformation($"AuctionService - {catalogueServiceUrl + getActivationEndpoint}");

                HttpResponseMessage activationResponse = await _httpClient.PutAsync(catalogueServiceUrl + getActivationEndpoint, null); // Send put request to specified endpoint

                _logger.LogInformation($"AuctionService - new Artifact.Status: {artifact.Status}");

                
                return Ok(result);
            }
            else
            {
                _logger.LogInformation("AuctionService - selected artifacID: " + artifact.ArtifactID);
                _logger.LogInformation("AuctionService - selected artifacName: " + artifact.ArtifactName);
                return BadRequest("AuctionService - Artifact is already on auction");
            }
        }
    }

    //RabbitMQ på AddNewBid!!!
    [HttpPost("addBid/{userId}/{auctionid}")] // DENNE METODE SKAL KØRE IGENNEM RABBIT, HOW???
    public async Task<IActionResult> AddNewBid([FromBody] Bid? bid, int userId, int auctionId)
    {
        _logger.LogInformation("AuctionService - AddNewBid function hit");

        var userResponse = await GetUserFromUserService(userId);


        if (userResponse.Result is ObjectResult objectResult && objectResult.Value is UserDTO user)
        {
            var latestId = await _auctionRepository.GetNextBidId();

            _logger.LogInformation("AuctionService - BidId: " + latestId);

            if (user != null)
            {

                var newBid = new Bid
                {
                    BidId = latestId,
                    ArtifactId = auctionId, //bid!.ArtifactId,
                    BidOwner = user,
                    BidAmount = bid!.BidAmount
                };
                _logger.LogInformation("AuctionService - new Bid object made. BidId: " + newBid.BidId);

                _auctionRepository.AddNewBid(newBid);


                var result = new
                {
                    AuctionId /*ArtifactId*/ = auctionId, //newBid.ArtifactId, //Ændret til AuctionId for at RabbitMQ modtager auctionid frem for artifactid
                    BidOwner = new
                    {
                        user.UserName,
                        user.UserEmail,
                        user.UserPhone
                    },
                    BidAmount = newBid.BidAmount,
                    BidDate = newBid.BidDate
                };
                //START HER FRA::
                var auction = await GetAuctionById(auctionId);

                await _auctionRepository.UpdateAuctionBid(auctionId, auction, newBid); //Rabbit if-sætning her i guess

                var aucton = await GetAuctionById(auctionId);

                int? currentBid = auction.CurrentBid;

                _logger.LogInformation("AuctionService - addNewBid - artifactID: " + auction.ArtifactID);

                _logger.LogInformation("AuctionService - addNewBid - bidAmount på newBid: " + newBid.BidAmount);
                _logger.LogInformation("AuctionService - addNewBid - bidAmount på bid: " + bid.BidAmount);

                if (newBid.BidAmount > currentBid)
                {
                    await _auctionRepository.UpdateAuctionBid(auctionId, auction, newBid);




                    // Publish the new artifact message to RabbitMQ
                    PublishNewBidMessage(result);


                    return Ok(result);
                }
                else
                {
                    return BadRequest($"Your bid of {newBid.BidAmount} is lower than the current bid");
                }
            }
            else
            {
                return BadRequest("AuctionService - User object is null");
            }
        }
        else
        {
            return BadRequest("AuctionService - Failed to retrieve User object");
        }
    }






    //PUT
    [HttpPut("updateAuction/{auctionId}"), DisableRequestSizeLimit]
    public async Task<IActionResult> UpdateAuction(int auctionId, [FromBody] Auction? auction)
    {
        _logger.LogInformation("AuctionService - UpdateAuction function hit");

        var updatedAuction = await _auctionRepository.GetAuctionById(auctionId);

        if (updatedAuction == null)
        {
            return BadRequest("AuctionService - Auction does not exist");
        }
        _logger.LogInformation("AuctionService - Auction for update: " + updatedAuction.AuctionId);

        await _auctionRepository.UpdateAuction(auctionId, auction!);

        var newUpdatedArtifact = await _auctionRepository.GetAuctionById(auctionId);

        return Ok($"Artifact, {updatedAuction.AuctionId}, has been updated. New AuctionEndDate: {newUpdatedArtifact.AuctionEndDate}");
    }






    //DELETE
    [HttpDelete("deleteAuction/{auctionId}"), DisableRequestSizeLimit]
    public async Task<IActionResult> DeleteAuction(int auctionId)
    {
        _logger.LogInformation("AuctionService - DeleteAuction function hit");

        var deletedAuction = await _auctionRepository.GetAuctionById(auctionId);

        if (deletedAuction == null)
        {
            return BadRequest("AuctionService - No auction with id: " + auctionId);
        }
        else if (deletedAuction.CurrentBid != null && deletedAuction.FinalBid == null)
        {
            return BadRequest("AuctionService - Cannot delete auction with active bids");
        }
        else await _auctionRepository.DeleteAuction(auctionId);
        _logger.LogInformation($"AuctionService - Auction with id: {auctionId} deleted");

        return Ok($"AuctionService - Auction has been deleted");
    }



}