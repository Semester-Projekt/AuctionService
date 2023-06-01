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
using RabbitMQ.Client.Events;
using MongoDB.Driver.Core.Bindings;
using System.Net.Http.Headers;
using System.Net.Http;

namespace Controllers;

[ApiController]
[Route("[controller]")]
public class AuctionController : ControllerBase
{
    private readonly ILogger<AuctionController> _logger;
    private readonly IConfiguration _config;
    private AuctionRepository _auctionRepository; // Enables calls to the methods and setup in the AuctionRepository

    public AuctionController(ILogger<AuctionController> logger, IConfiguration config, AuctionRepository auctionRepository)
    {
        _config = config;
        _logger = logger;
        _auctionRepository = auctionRepository;
        _logger.LogInformation($"Connecting to rabbitMQ on {_config["rabbithostname"]}"); //tester om den kommer på rigtig rabbitserver. Skrives i logs.

        //Logger host information
        var hostName = System.Net.Dns.GetHostName();
        var ips = System.Net.Dns.GetHostAddresses(hostName);
        var _ipaddr = ips.First().MapToIPv4().ToString();
        _logger.LogInformation(1, $"Auth service responding from {_ipaddr}");
    }

    //RabbitMQ start
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
            // Declare a queue. Sender queue
            channel.QueueDeclare(queue: "new-bid-queue",
                                 durable: false,
                                 exclusive: false,
                                 autoDelete: false,
                                 arguments: null);


            // Convert newArtifact to a JSON string
            var json = JsonSerializer.Serialize(result);

            // Publish the message to the queue
            var body = Encoding.UTF8.GetBytes(json);
            channel.BasicPublish(exchange: "", routingKey: "new-bid-queue", basicProperties: null, body: body);
        }

        // Return the result object
        return result;
    }
    //RabbitMQ slut


    // VERSION_ENDPOINT
    [HttpGet("version")]
    public async Task<Dictionary<string, string>> GetVersion()
    {
        // Create a dictionary to store the version-related properties
        var properties = new Dictionary<string, string>();
        var assembly = typeof(Program).Assembly;

        // Add the service name to the properties
        properties.Add("service", "Auction");

        // Get the version information of the program assembly and add it to the properties
        var ver = FileVersionInfo.GetVersionInfo(typeof(Program).Assembly.Location).ProductVersion;
        properties.Add("version", ver!);

        try
        {
            // Get the host name and its corresponding IP addresses
            var hostName = System.Net.Dns.GetHostName();
            var ips = await System.Net.Dns.GetHostAddressesAsync(hostName);

            // Convert the first IPv4 address to a string and add it to the properties
            var ipa = ips.First().MapToIPv4().ToString();
            properties.Add("hosted-at-address", ipa);
        }
        catch (Exception ex)
        {
            // Log the exception if an error occurs while retrieving the IP address
            _logger.LogError(ex.Message);

            // Add a fallback message to the properties if the IP address cannot be resolved
            properties.Add("hosted-at-address", "Could not resolve IP-address");
        }

        // Return the populated properties dictionary
        return properties;
    }







    //GET
    [Authorize]
    [HttpGet("getallauctions")]
    public async Task<IActionResult> GetAllAuctions()
    {
        _logger.LogInformation("AuctionService - GetAllAuctions function hit");

        var auctions = await _auctionRepository.GetAllAuctions();

        _logger.LogInformation("AuctionService - Total auctions: " + auctions.Count());

        if (auctions == null)
        {
            return BadRequest("AuctionService - Auction list is empty");
        }
        
        return Ok(auctions);
    }

    [Authorize]
    [HttpGet("getAuctionById/{auctionId}")]
    public async Task<Auction> GetAuctionById(int auctionId)
    {
        _logger.LogInformation("AuctionService - GetAuctionById function hit");

        var auction = await _auctionRepository.GetAuctionById(auctionId);

        var bidHistory = _auctionRepository.GetAllBids().Result.Where(b => b.ArtifactId == auction.ArtifactID);
        auction.BidHistory = (List<Bid>?)bidHistory.OrderByDescending(b => b.BidDate).ToList();

        int? finalBid;
        if (auction.AuctionEndDate < DateTime.Now)
        {
            // In case of an expired Auction, the current highest bid is set as the FinalBid
            finalBid = _auctionRepository.GetAllBids().Result.Where(b => b.ArtifactId == auction.ArtifactID).OrderByDescending(b => b.BidAmount).FirstOrDefault()!.BidAmount;
        }
        else
        {
            finalBid = null; // In case of an ongoing Auction, FinalBid remains null
        }
        
        return auction;
    }

    // Helper method for retreiving a UserDTO object from UserService; is used when connecting a User to a Bid
    [HttpGet("getUserFromUserService/{userName}"), DisableRequestSizeLimit]
    public async Task<ActionResult<UserDTO>> GetUserFromUserService(string userName)
    {
        _logger.LogInformation("AuctionService - GetUser function hit");

        using (HttpClient client = new HttpClient()) // Creates an instance of the HttpClient class to send HTTP requests
        {
            // Get the URL of the UserService from environment variables in the Service Deployment file
            string userServiceUrl = Environment.GetEnvironmentVariable("USER_SERVICE_URL")!;
            // Prepare the endpoint to retrieve a specific user
            string getUserEndpoint = "/user/getUser/" + userName;

            _logger.LogInformation(userServiceUrl + getUserEndpoint);

            // Send a GET request to the user service to retrieve the user
            HttpResponseMessage response = await client.GetAsync(userServiceUrl + getUserEndpoint);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "AuctionService - Failed to retrieve UserId from UserService");
            }

            // Read the response content as a UserDTO object
            var userResponse = await response.Content.ReadFromJsonAsync<UserDTO>();

            if (userResponse != null)
            {
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
    [Authorize]
    [HttpPost("addauction/{artifactID}")]
    public async Task<IActionResult> AddAuctionFromArtifactId(int artifactID)
    {
        _logger.LogInformation("AuctionService - AddAuctionFromArtifactId function hit");

        using (HttpClient _httpClient = new HttpClient()) // Creates an instance of the HttpClient class to send HTTP requests
        {
            // Get the URL of the CatalogueService from environment variables in the Service Deployment file
            string catalogueServiceUrl = Environment.GetEnvironmentVariable("CATALOGUE_SERVICE_URL")!;
            // Prepare the endpoint to retrieve a specific user
            string getCatalogueEndpoint = "/catalogue/getArtifactById/" + artifactID;

            _logger.LogInformation($"AuctionService: {catalogueServiceUrl + getCatalogueEndpoint}");

            // Retrieves the value of the "Authorization" header from the current HTTP request.
            var tokenValue = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
            _logger.LogInformation("AuctionService - token first default: " + tokenValue);
            var token = tokenValue?.Replace("Bearer ", ""); // Removes the "Bearer " prefix from the token value, if present.
            _logger.LogInformation("AuctionService - token w/o bearer: " + token);
            
            // Creates an HTTP request message to perform a GET request to the specified URL.
            var request = new HttpRequestMessage(HttpMethod.Get, catalogueServiceUrl + getCatalogueEndpoint);

            // Adds the authentication header with the modified token value to the request.
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Sends the HTTP request and awaits the response.
            HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "AuctionService - Failed to retrieve ArtifactID from ArtifactService");
            }

            // Reads the content of the response and deserializes it into an ArtifactDTO object.
            var artifact = response.Content.ReadFromJsonAsync<ArtifactDTO>().Result!;

            // Get all existing artifacts and determines the latest ID
            var allArtifacts = await _auctionRepository.GetAllAuctions();
            int? latestID = allArtifacts.DefaultIfEmpty().Max(a => a == null ? 0 : a.ArtifactID) + 1;

            var newAuction = new Auction
            {
                AuctionId = (int)latestID,
                ArtifactID = artifactID
            };

            // The logic inside this if-statement is only executed if the retreive Artifact is not already on auction
            if (artifact.Status != "Active")
            {
                _logger.LogInformation("AuctionService - New Auction object added");

                _logger.LogInformation($"result: ArtifactID: {newAuction.ArtifactID} + AuctionEndDate: {newAuction.AuctionEndDate}");

                _logger.LogInformation($"AuctionService - current Artifact.Status: {artifact.Status}");

                // Specify activateArtifact endpoint to change Artifact status to 'Active'
                string getActivationEndpoint = "/catalogue/activateArtifact/" + artifactID;

                _logger.LogInformation($"AuctionService - {catalogueServiceUrl + getActivationEndpoint}");

                // Send put request to specified endpoint, which updates the Artifact.Status to 'Active'
                HttpResponseMessage activationResponse = await _httpClient.PutAsync(catalogueServiceUrl + getActivationEndpoint, null);

                _auctionRepository.AddNewAuction(newAuction);

                return Ok(newAuction);
            }
            else
            {
                // In case the Artifact.Status is already 'Active', a BadRequest is returned and user is informed of the already ongoing Auction
                return BadRequest("AuctionService - Artifact is already on auction");
            }
        }
    }

    //RabbitMQ på AddNewBid!!!
    [Authorize]
    [HttpPost("addBid/{userName}/{auctionid}")] // DENNE METODE SKAL KØRE IGENNEM RABBIT, HOW???
    public async Task<IActionResult> AddNewBid([FromBody] Bid? bid, string userName, int auctionId)
    {
        _logger.LogInformation("AuctionService - AddNewBid function hit");

        var userResponse = await GetUserFromUserService(userName);


        if (userResponse.Result is ObjectResult objectResult && objectResult.Value is UserDTO user)
        {
            var latestId = await _auctionRepository.GetNextBidId();

            _logger.LogInformation("AuctionService - BidId: " + latestId);

            if (user != null)
            {

                // Retrieve the bidAmount value from RabbitMQ
                var bidAmount = bid?.BidAmount ?? 0; // Assume a default value if bid or bidAmount is null

                var newBid = new Bid
                {
                    BidId = latestId,
                    ArtifactId = auctionId, //bid!.ArtifactId,
                    BidOwner = user,
                    BidAmount = bidAmount //Set bidAmount to value from RabbitMQ
                };

                _logger.LogInformation("AuctionService - new Bid object made. BidId: " + newBid.BidId);

                _auctionRepository.AddNewBid(newBid);


                var result = new
                {
                    AuctionId = auctionId, //Ændret til AuctionId for at RabbitMQ modtager auctionid frem for artifactid
                    BidOwner = new
                    {
                        user.UserName,
                        user.UserEmail,
                        user.UserPhone
                    },
                    BidAmount = newBid.BidAmount,
                    BidDate = newBid.BidDate
                };

                var auction = await GetAuctionById(auctionId);

                await _auctionRepository.UpdateAuctionBid(auctionId, auction, newBid); //Rabbit if-sætning her i guess

                int? currentBid = auction.CurrentBid;

                _logger.LogInformation("AuctionService - addNewBid - artifactID: " + auction.ArtifactID);

                _logger.LogInformation("AuctionService - addNewBid - bidAmount på newBid: " + newBid.BidAmount);
                _logger.LogInformation("AuctionService - addNewBid - bidAmount på bid: " + bid!.BidAmount);

                if (newBid.BidAmount > currentBid)
                {
                    await _auctionRepository.UpdateAuctionBid(auctionId, auction, newBid);

                    _logger.LogInformation("AuctionService - BidAmount updated and bid added");

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
    [Authorize]
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
    [Authorize]
    [HttpDelete("deleteAuction/{auctionId}"), DisableRequestSizeLimit]
    public async Task<IActionResult> DeleteAuction(int auctionId)
    {
        _logger.LogInformation("AuctionService - DeleteAuction function hit");

        var deletedAuction = await _auctionRepository.GetAuctionById(auctionId);
        
        if (deletedAuction == null)
        {
            return BadRequest("AuctionService - No auction with id: " + auctionId);
        }
        // Validates whether there are active bids on the Auction
        else if (deletedAuction.CurrentBid > 0 && deletedAuction.FinalBid == null)
        {
            return BadRequest("AuctionService - Cannot delete auction with active bids");
        }
        else
        {
            using (HttpClient _httpClient = new HttpClient()) // Creates an instance of the HttpClient class to send HTTP requests
            {
                // Get the URL of the CatalogueService from environment variables in the Service Deployment file
                string catalogueServiceUrl = Environment.GetEnvironmentVariable("CATALOGUE_SERVICE_URL")!;
                // Prepare the deleteArtifact endpoint
                string getDeletionEndpoint = "/catalogue/deleteartifact/" + deletedAuction.ArtifactID;

                _logger.LogInformation($"AuctionService: {catalogueServiceUrl + getDeletionEndpoint}");

                // See AddAuction for explanation
                var tokenValue = HttpContext.Request.Headers["Authorization"].FirstOrDefault();
                _logger.LogInformation("AuctionService - token first default: " + tokenValue);
                var token = tokenValue?.Replace("Bearer ", "");
                _logger.LogInformation("AuctionService - token w/o bearer: " + token);

                // Create a new HttpRequestMessage to include the token
                var request = new HttpRequestMessage(HttpMethod.Put, catalogueServiceUrl + getDeletionEndpoint);
                //request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Sends the Http request to CatalogueService which changes the Artifact.Status of the related Artifact to 'Deleted'
                HttpResponseMessage response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode((int)response.StatusCode, "AuctionService - Failed to retrieve Delete Artifact from ArtifactService");
                }
                
                await _auctionRepository.DeleteAuction(auctionId);
                _logger.LogInformation($"AuctionService - Auction with id: {auctionId} deleted");

                return Ok($"AuctionService - Auction has been deleted");
            }

        }



    }



}