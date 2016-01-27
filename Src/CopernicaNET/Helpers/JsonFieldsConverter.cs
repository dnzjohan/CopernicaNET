using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Arlanet.CopernicaNET.Attributes;
using Arlanet.CopernicaNET.Data;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Arlanet.CopernicaNET.Helpers
{
    public class JsonFieldsConverter: JsonConverter
    {
        
        public override bool CanConvert(Type objectType)
        {
            return true;
        }

	    //This json converter is used to map the actual names that are given in the CopernicaField attributes.
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            //TODO: Cleanup!

            // Load JObject from stream and get the data object, it's where the actual information is.
            JObject jObject = JObject.Load(reader);
            var data = jObject["data"];

            //If data is empty the REST api returned nothing
	        if (data.FirstOrDefault() == null)
		        return null;

            //This part is needed when retrieving the fields in order to validate the given object.
            if (objectType.Name == "CopernicaField")
            {
                //Create a list of CopernicaField to return
                return data.Select(item => new CopernicaField(item["name"].ToString())
                {
                    //All we need to know is the Name, Length and Type to validate.
                    Length = item["length"] == null ? 0 : (int)item["length"], 
                    Type = (string) item["type"]
                }).ToList();
            }

	        if (data.Type == JTokenType.Array)
	        {
		        var listType = typeof (List<>);
				// Create list
		        var obj = Activator.CreateInstance(listType.MakeGenericType(objectType));
				// Get add method
				MethodInfo method = obj.GetType().GetMethod("Add");

		        //parse array of items
		        foreach (var dataItem in data)
		        {
					var deserializedItem = DeserializeJsonObject(objectType, serializer, JObject.Parse(dataItem.ToString()));
					// Add deserializedItem to List
					method.Invoke(obj, new[] { deserializedItem });
		        }
		        return obj;
	        }

	        //parse single item
			return DeserializeJsonObject(objectType, serializer, JObject.Parse(data.First.ToString()));
        }

	    private static object DeserializeJsonObject(Type objectType, JsonSerializer serializer, dynamic data)
	    {
		    dynamic id = Int32.Parse(data["ID"].ToString());
		    dynamic b = JObject.Parse(data["fields"].ToString());

		    var obj = (Object) Activator.CreateInstance(objectType);
		    var jobject = new JObject(b);

		    //Get all the properties and loop trough them. Then add all the properties with the correct names from je object.
		    //This makes sure the mapping is right when deserializing the object.
		    var properties =
			    objectType.GetProperties()
				    .Where(
					    x =>
						    x.GetCustomAttributes(false)
							    .Any(y => y.GetType() == typeof (CopernicaField) || y.GetType() == typeof (CopernicaKeyField)));
		    var jobj = new JObject();
		    foreach (var property in properties)
		    {
			    jobj.Add(property.Name, jobject[property.GetCustomAttribute<CopernicaField>().Name]);
		    }

		    //Populate the correct data in the object.
		    JsonReader objreader = jobj.CreateReader();
		    serializer.Populate(objreader, obj);

		    var idProperty = objectType.GetProperties().FirstOrDefault(p => p.Name == "ID");

		    if (idProperty != null)
		    {
			    idProperty.SetValue(obj, id);
		    }
		    return obj;
	    }

	    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value == null)
            {
                serializer.Serialize(writer, null);
                return;
            }

            //Get all the properties that contain either the CopernicaField of the CopernicaKeyField attribute.
            var properties = value.GetType().GetProperties().Where(x => x.GetCustomAttributes(false).Any(y => y.GetType() == typeof(CopernicaField) || y.GetType() == typeof(CopernicaKeyField)));
            
            //Loop through the properties and add the CopernicaField name + property value to the JObject.
            //This makes sure the mapping is right when serializing the object.
            JObject obj = new JObject();
            foreach (var property in properties)
            {
                obj.Add(property.GetCustomAttribute<CopernicaField>().Name, property.GetValue(value) == null ? "" : property.GetValue(value).ToString());
            }
            obj.WriteTo(writer);

            writer.Flush();
        }
	}
}
