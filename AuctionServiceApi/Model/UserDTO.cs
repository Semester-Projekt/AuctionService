using MongoDB.Driver;
using System.Threading.Tasks;
using Model;
using System;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Model
{
    public class UserDTO
    {
        [BsonId] // mongo id for a specified user
        [BsonRepresentation(BsonType.ObjectId)]
        public string? MongoId { get; set; }

        [BsonElement("UserId")]
        public int? UserId { get; set; }

        [BsonElement("UserName")]
        public string? UserName { get; set; }

        [BsonElement("UserPassword")]
        public string? UserPassword { get; set; }

        [BsonElement("UserEmail")]
        public string? UserEmail { get; set; }

        [BsonElement("UserPhone")]
        public int? UserPhone { get; set; }

        [BsonElement("UserAddress")]
        public string? UserAddress { get; set; }


        public UserDTO(int userId, string userName, string userPassword, string userEmail, int userPhone, string userAddress)
        {
            this.UserId = userId;
            this.UserName = userName;
            this.UserPassword = userPassword;
            this.UserEmail = userEmail;
            this.UserPhone = userPhone;
            this.UserAddress = userAddress;
        }

        public UserDTO()
        {

        }
    }
}