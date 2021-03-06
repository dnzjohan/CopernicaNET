﻿using System;
using System.Collections.Generic;
using System.Linq;
using Arlanet.CopernicaNET.Attributes;
using Arlanet.CopernicaNET.Configuration;
using Arlanet.CopernicaNET.Data;
using Arlanet.CopernicaNET.Helpers;
using Arlanet.CopernicaNET.Interfaces.Data;
using Arlanet.CopernicaNET.Interfaces.Handlers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;

namespace Arlanet.CopernicaNET
{
    public class Copernica<T> where T : ICopernicaHandlerBase, new()
    {
        #region Constructors
		public Copernica()
			: this(CopernicaSettings.Settings.AccessToken)
		{
		}

        public Copernica(string accesstoken)
        {
            _accesstoken = accesstoken;
            _dataHandler = new CopernicaDataHandler();
        }

        public Copernica(string accesstoken, ICopernicaDataHandler handler)
        {
            _accesstoken = accesstoken;
            _dataHandler = handler;
        }

        #endregion

        #region Class Variables

        private static T _instance;

        private static string _accesstoken;

        private static ICopernicaDataHandler _dataHandler;

        #endregion

        #region Properties
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new T();
                    _instance.RegisterDataItems();
                }

                return _instance;
            }
        }
        #endregion

        #region Validator Helper Methods

        /// <summary>
        /// Validates the profile. It makes sure the field's name, length and type are the same as in the copernica database.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <returns></returns>
        private static void ValidateProfile(ICopernicaProfile profile)
        {
            //Retrieve the fields from the copernica database and deserialize them to the CopernicaField objects. The custom json converter handles the deserialization.
            var response = _dataHandler.GetProfileFields(profile.DatabaseId, _accesstoken);
            List<CopernicaField> copernicafields = new List<CopernicaField>();
            var dbid = profile.DatabaseId;

            try
            {
                copernicafields =
                    (List<CopernicaField>) JsonConvert.DeserializeObject(response, typeof (CopernicaField));
            }
            catch (Exception ex)
            {
                throw new CopernicaException("Unable to retrieve the fields. Make sure the specified database identifier is correct.", ex);
            }

            //Retrieve the fields from the object and compare them with the fields from the copernica database.
            IEnumerable<CopernicaField> objectfields = Validator.GetObjectFields(profile);

            //TODO: Validate Required also!
            Validator.CompareFields(copernicafields, objectfields);
        }

        private static void ValidateSubprofile(ICopernicaSubprofile subprofile)
        {
            //var response = RequestHandler.Get(String.Format("subprofile/{0}/fields?access_token={1}", subprofile.DatabaseId, _accesstoken));
            //TODO: validate same as profile

            //Retrieve the fields from the copernica database and deserialize them to the CopernicaField objects. The custom json converter handles the deserialization.
            var response = _dataHandler.GetSubProfileFields(subprofile.CollectionId, _accesstoken);
            var copernicafields = (List<CopernicaField>)JsonConvert.DeserializeObject(response, typeof(CopernicaField));

            //Retrieve the fields from the object and compare them with the fields from the copernica database.
            IEnumerable<CopernicaField> objectfields = Validator.GetObjectFields(subprofile);

            Validator.CompareFields(copernicafields, objectfields);
        }

        #endregion

        #region Protected Methods
       
        //All the data items should be registered when using the Copernica .NET library. For validating purposes.       
        /// <summary>
        /// Registers the data item and validates it. This also means mapping it with the CopernicaDatabase.
        /// </summary>
        /// <param name="dataItem">The data item.</param>
        protected void RegisterDataItem(ICopernicaDataItem dataItem)
        {
            if (dataItem is ICopernicaProfile)
                ValidateProfile(dataItem as ICopernicaProfile);

            if (dataItem is ICopernicaSubprofile)
                ValidateSubprofile(dataItem as ICopernicaSubprofile);
        }
        #endregion
        
        #region Public Methods

        /// <summary>
        /// Updates the profile using the CopernicaKeyField as identifier. Multiple rows will be updated if multiple rows are found with the same identifier, which is not supposed to happen.
        /// </summary>
        /// <param name="profile"></param>
        /// <returns></returns>
        public void Update(ICopernicaProfile profile)
        {
            var value = profile.GetKeyFieldValue();

            if (value != "0")
            {
                string keyname = profile.GetKeyFieldName();
                string keyvalue = profile.GetKeyFieldValue();
                string jsondata = JsonConvert.SerializeObject(profile);
                _dataHandler.UpdateProfile(profile.DatabaseId, keyname, keyvalue, jsondata, _accesstoken);
            }
        }

        /// <summary>
        /// Updates the specified profiles.
        /// </summary>
        /// <param name="profiles">The profiles.</param>
        /// <exception cref="System.NullReferenceException">The list you're trying to insert is empty.</exception>
        public void Update(IEnumerable<ICopernicaProfile> profiles)
        {
            if(profiles == null)
                throw new CopernicaException("The list you're trying to insert is empty.");

            //Loops trough the profiles and updates them. This is because the REST api doesn't allow POSTing lists.
            foreach (ICopernicaProfile profile in profiles)
            {
                Update(profile);
            }
        }

        /// <summary>
        /// Adds the specified profile to the copernica database.
        /// </summary>
        /// <param name="profile">The profile.</param>
        public void Add(ICopernicaProfile profile)
        {
            //Serialize the profile and post it.
            string jsondata = JsonConvert.SerializeObject(profile);

            if(profile.GetKeyFieldValue() == "0")
                throw new CopernicaException("The specified identifier is empty.");

            _dataHandler.CreateProfile(profile.DatabaseId, jsondata, _accesstoken);
        }

        /// <summary>
        /// Adds the specified subprofile to the referenced profile.
        /// </summary>
        /// <param name="subprofile">The subprofile.</param>
        /// <param name="refprofile">The reference profile.</param>
        public void Add(ICopernicaSubprofile subprofile, ICopernicaProfile refprofile)
        {
            string jsondata = JsonConvert.SerializeObject(subprofile);
            var id = GetCopernicaProfileId(refprofile);
            _dataHandler.CreateSubProfile(subprofile.CollectionId, id, jsondata, _accesstoken);
        }

        /// <summary>
        /// Deletes the specified profile.
        /// </summary>
        /// <param name="profile">The profile.</param>
        public void Delete(ICopernicaProfile profile)
        {
            var id = GetCopernicaProfileId(profile);
            _dataHandler.DeleteProfile(id, _accesstoken);
        }

        /// <summary>
        /// Gets the profile by key.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="inputprofile"></param>
        /// <returns></returns>
        public virtual T GetProfileByKey<T>(T inputprofile) where T : ICopernicaProfile, new()
        {
            //ValidateProfile<T>(inputprofile);
            //TODO: Zorgen dat er meer identifiers kunnen! + Toevoegen aan documentatie
            //Retrieve the key name and value used to identify the profile.
            var keyname = inputprofile.GetKeyFieldName();
            var keyvalue = inputprofile.GetKeyFieldValue();

            //Request the profile
            var response = _dataHandler.GetProfileByKey(inputprofile.DatabaseId, keyname, keyvalue, _accesstoken);
            dynamic data = JObject.Parse(response);
            try
            {
                //Convert it to an object and return it as T
                object outputprofile = JsonConvert.DeserializeObject(response, typeof (T));
	            if (outputprofile == null)
					return default(T); // return null
				if (outputprofile.GetType().Equals(typeof(T)))
					return (T) outputprofile;
	            return ((List<T>) outputprofile).FirstOrDefault();
            }
            catch (NullReferenceException)
            {
                throw new CopernicaException("The profile was not found.");
            }
        }


		public virtual IEnumerable<T> GetCollectionByProfileId<T>(T subProfile) where T : ICopernicaSubprofile, new()
		{
			//Request the collection
			var response = _dataHandler.GetCollectionByProfileId(subProfile.DatabaseId, subProfile.ProfileId, subProfile.CollectionId, _accesstoken);
			dynamic data = JObject.Parse(response);
			try
			{
				//Convert it to an object and return it as T
				object outputCollection = JsonConvert.DeserializeObject(response, typeof(T));
				return (List<T>)outputCollection;
			}
			catch (NullReferenceException)
			{
				throw new CopernicaException("The collection was not found.");
			}
		}

        /// <summary>
        /// Gets the copernica profile identifier, this is not the custom identifier but the one generated by copernica.
        /// It does however retrieve the key using the custom identifier specified in the object.
        /// </summary>
        /// <param name="profile">The profile.</param>
        /// <returns></returns>
        /// <example>
        /// </example>
        /// 
        private int GetCopernicaProfileId(ICopernicaProfile profile)
        {
            Dictionary<string,string> keys = profile.GetKeys();
            var response = _dataHandler.GetProfileByKey(profile.DatabaseId, profile.GetKeyFieldName(), profile.GetKeyFieldValue(), _accesstoken);

            //var response = _dataHandler.GetProfileByKey(profile.DatabaseId, keyname, keyvalue, _accesstoken);
            dynamic data = JObject.Parse(response);

            try
            {
                //TODO: Gooi exception als er meer records gevonden zijn.
                //TODO: Check hoe de GET string geformat moet worden om de data te vinden met meerdere keys(doet nu alleen met 1 field)
                //TODO: Documentatie afmaken! sandcastlehelpfilebuilder
                //TODO: Required checken van fields!
                //TODO: Checken of de accesstoken klopt. Exception gooien zo niet!
                //TODO: Test scenarios maken voor validatie
                //TODO: Validate fields which can only be used ones per database.
                //TODO: Multiple rows are updated if they have the same identifier
                var id = data.data[0].ID.Value;
                return Int32.Parse(id);
            }
            catch (Exception ex)
            {
                throw new CopernicaException("The profile was not found.", ex);
            }
        }

        #endregion
    }
}