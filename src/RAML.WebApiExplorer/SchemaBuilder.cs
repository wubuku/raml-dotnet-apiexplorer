using System.Globalization;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace RAML.WebApiExplorer
{
	public class SchemaBuilder
	{
        private readonly IDictionary<string, Type> definitions = new Dictionary<string, Type>();
        private readonly ICollection<Type> types = new Collection<Type>();


		public string Get(Type type)
		{
            definitions.Clear();
            types.Clear();

		    string schema;

			if (type.IsGenericType && IsGenericWebResult(type))
				type = type.GetGenericArguments()[0];

		    if (IsArrayOrEnumerable(type))
		    {
		        var elementType = type.GetElementType() ?? type.GetGenericArguments()[0];
		        if (elementType.GetProperties().Count(p => p.CanWrite && p.SetMethod.IsPublic) == 0)
		            return null;

		        schema = GetMainArraySchema(elementType);
		    }
		    else
		    {
		        if (type.GetProperties().Count(p => p.CanWrite && p.SetMethod.IsPublic) == 0)
		            return null;

		        schema = GetMainObjectSchema(type);
		    }

		    schema = AddDefinitionsIfAny(schema);

            schema += "}" + Environment.NewLine;

		    return schema;
		}

	    private string AddDefinitionsIfAny(string schema)
	    {
	        if (definitions.Any())
	        {
	            schema = AddTrailingComma(schema);
	            schema += GetDefinitions();
	        }
	        return schema;
	    }

	    private static string AddTrailingComma(string schema)
	    {
            if(!String.IsNullOrWhiteSpace(schema) && schema.Length > "\r\n".Length)
                return schema.Substring(0, schema.Length - "\r\n".Length) + "," + Environment.NewLine;

	        return schema;
	    }

	    private string GetDefinitions()
	    {
	        var schema = "  \"definitions\": {" + Environment.NewLine;
	        foreach (var definition in definitions)
	        {
	            schema += "    \"" + definition.Key + "\": {" + Environment.NewLine;
                schema += "      \"properties\": {" + Environment.NewLine;
	            schema += GetProperties(definition.Value, 8);
	            schema += "      }";
                
                if(definition.Key == definitions.Last().Key)
                    schema += "    }" + Environment.NewLine;
                else
                    schema += "    }," + Environment.NewLine;
	        }
	        schema = RemoveTrailingComma(schema);
            schema += "  }" + Environment.NewLine;
	        return schema;
	    }

	    private static string RemoveTrailingComma(string schema)
	    {
	        schema = schema.Substring(0, schema.Length - 2) + Environment.NewLine;
	        return schema;
	    }

	    private string GetMainObjectSchema(Type type)
	    {
	        var objectSchema = "{ \r\n" +
	                           "  \"$schema\": \"http://json-schema.org/draft-03/schema\",\r\n" +
	                           "  \"type\": \"object\",\r\n" +
                               "  \"id\": \"" + type.Name + "\",\r\n" +
	                           "  \"properties\": {\r\n";

            types.Add(type);

	        objectSchema += GetProperties(type, 4);

	        objectSchema += "  }\r\n";

	        return objectSchema;
	    }

	    private string GetMainArraySchema(Type elementType)
	    {
	        var arraySchema = "{\r\n" +
	                          "  \"$schema\": \"http://json-schema.org/draft-03/schema\",\r\n" +
	                          "  \"type\": \"array\",\r\n" +
	                          "  \"items\": \r\n" +
	                          "  {\r\n" +
	                          "    \"type\": \"object\",\r\n" +
                              "    \"id\": \"" + elementType.Name + "\",\r\n" +
	                          "    \"properties\": \r\n" +
	                          "    {\r\n";
            types.Add(elementType);

	        arraySchema += GetProperties(elementType, 6);

	        arraySchema += "    }\r\n";
	        arraySchema += "  }\r\n";
	        return arraySchema;
	    }

	    private static bool IsGenericWebResult(Type type)
		{
			return (type.GetGenericTypeDefinition() == typeof (System.Web.Http.Results.OkNegotiatedContentResult<>)
					|| type.GetGenericTypeDefinition() == typeof(System.Web.Http.Results.NegotiatedContentResult<>)
					|| type.GetGenericTypeDefinition() == typeof(System.Web.Http.Results.CreatedAtRouteNegotiatedContentResult<>)
					|| type.GetGenericTypeDefinition() == typeof(System.Web.Http.Results.CreatedNegotiatedContentResult<>)
					|| type.GetGenericTypeDefinition() == typeof(System.Web.Http.Results.FormattedContentResult<>)
					|| type.GetGenericTypeDefinition() == typeof(System.Web.Http.Results.JsonResult<>));
		}

		private string GetRecursively(Type type, int pad, IEnumerable<CustomAttributeData> customAttributes)
		{
			if (type.IsGenericType && IsGenericWebResult(type))
				type = type.GetGenericArguments()[0];

		    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof (Dictionary<,>))
                return null;

            if (IsArrayOrEnumerable(type))
			{
				var elementType = type.GetElementType() ?? type.GetGenericArguments()[0];

			    if (SchemaTypeMapper.Map(elementType) != null)
			        return GetNestedArraySchemaPrimitiveType(pad, elementType);

    	         if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof (KeyValuePair<,>))
                        return GetNestedArraySchema(pad, elementType);

				if (elementType.GetProperties().Count(p => p.CanWrite && p.SetMethod.IsPublic) == 0)
					return string.Empty;

			    return GetNestedArraySchema(pad, elementType);
			}
		    
            if (type.GetProperties().Count(p => p.CanWrite && p.SetMethod.IsPublic) == 0)
		        return string.Empty;

		    return GetNestedObjectSchema(type, pad, customAttributes);
		}

	    private string GetNestedObjectSchema(Type type, int pad, IEnumerable<CustomAttributeData> customAttributes)
	    {
	        if (types.Contains(type))
	        {
                return ("{ \"$ref\": \"" + type.Name + "\" }").Indent(pad) + Environment.NewLine;
	        }

            types.Add(type);
	        
            var attributes = HandleRequiredAttribute(customAttributes);
            attributes += HandleValidationAttributes(customAttributes);

	        var objectSchema = "{ \r\n".Indent(pad) +
	                           "  \"type\": \"object\",\r\n".Indent(pad) +
                               (string.IsNullOrWhiteSpace(attributes) ? "" : ("  " + attributes + ",\r\n").Indent(pad)) +
                               ("  \"id\": \"" + type.Name + "\",\r\n").Indent(pad) + 
                               "  \"properties\": {\r\n".Indent(pad);

	        objectSchema += GetProperties(type, pad + 4);

	        objectSchema += "  }\r\n".Indent(pad);
	        objectSchema += "}\r\n".Indent(pad);
	        return objectSchema;
	    }

	    private string GetNestedArraySchema(int pad, Type elementType)
	    {
	        var arraySchema = "{\r\n".Indent(pad) +
	                          "  \"type\": \"array\",\r\n".Indent(pad) +
	                          "  \"items\": \r\n".Indent(pad);

	        if (types.Contains(elementType))
	        {
                arraySchema += ("{ \"$ref\": \"" + elementType.Name + "\" }").Indent(pad + 4) + Environment.NewLine;
	        }
	        else
	        {
                arraySchema += "  {\r\n".Indent(pad) +
                               "    \"type\": \"object\",\r\n".Indent(pad) +
                               "    \"properties\": \r\n".Indent(pad) +
                               "    {\r\n".Indent(pad);

	            if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof (KeyValuePair<,>))
	            {
                    var keyType = SchemaTypeMapper.Map(elementType.GetGenericArguments()[0]) ?? "object";
                    var valueType = SchemaTypeMapper.Map(elementType.GetGenericArguments()[1]) ?? "object";
	                arraySchema += ("\"Key\": { \"type\": \"" + keyType + "\"},\r\n").Indent(pad + 4) +
	                               ("\"Value\" : { \"type\": \"" + valueType + "\"}\r\n").Indent(pad + 4);
	            }
	            else
	            {
                    types.Add(elementType);
	                arraySchema += GetProperties(elementType, pad + 4);
	            }
	            arraySchema += "    }\r\n".Indent(pad);
                arraySchema += "  }\r\n".Indent(pad);            
	        }

	        arraySchema += "}\r\n".Indent(pad);
	        return arraySchema;
	    }

        private string GetNestedArraySchemaPrimitiveType(int pad, Type elementType)
        {
            var arraySchema = "{\r\n".Indent(pad) +
                              "  \"type\": \"array\",\r\n".Indent(pad) +
                              "  \"items\": \r\n".Indent(pad) +
                              "  {\r\n".Indent(pad) +
                              ("    \"type\": " + SchemaTypeMapper.GetAttribute(elementType) + "\r\n").Indent(pad) +
                              "  }\r\n".Indent(pad) +
                              "}\r\n".Indent(pad);
            return arraySchema;
        }

	    private string GetProperties(Type elementType, int pad)
		{
			var schema = string.Empty;
			var props = elementType.GetProperties().Where(p => p.CanWrite && p.SetMethod.IsPublic).ToArray();
			foreach (var prop in props)
			{
				schema = GetProperty(pad, prop, schema, props, prop.CustomAttributes);
			}
			return schema;
		}

	    private string GetProperty(int pad, PropertyInfo prop, string schema, IEnumerable<PropertyInfo> props, IEnumerable<CustomAttributeData> customAttributes)
	    {
	        if (prop.PropertyType.IsEnum)
	            schema = HandleEnumProperty(pad, prop, props, schema);
	        else if (SchemaTypeMapper.Map(prop.PropertyType) != null)
	            schema = HandlePrimitiveTypeProperty(pad, prop, props, schema, customAttributes);
	        else 
                schema = HandleNestedTypeProperty(pad, prop, schema, props, customAttributes);
	        
            return schema;
	    }

	    private string HandleEnumProperty(int pad, PropertyInfo prop, IEnumerable<PropertyInfo> props, string schema)
	    {
            if (prop == props.Last())
                schema += GetEnumProperty(prop, pad) + Environment.NewLine;
            else
                schema += GetEnumProperty(prop, pad) + "," + Environment.NewLine;
            return schema;
	    }

	    private static string GetEnumProperty(PropertyInfo prop, int pad)
	    {
            return ("\"" + GetPropertyName(prop) + "\": { ").Indent(pad) + Environment.NewLine
                + ("  \"enum\": [" + string.Join(", ", Enum.GetNames(prop.PropertyType).Select(v => "\"" + v + "\"")) + "]").Indent(pad) + Environment.NewLine
                + "}".Indent(pad);
	    }

	    private static string HandlePrimitiveTypeProperty(int pad, PropertyInfo prop, IEnumerable<PropertyInfo> props, string schema, IEnumerable<CustomAttributeData> customAttributes)
	    {
	        if (prop == props.Last())
	            schema += BuildLastProperty(prop, customAttributes, pad);
	        else
	            schema += BuildProperty(prop, customAttributes, pad);
	        return schema;
	    }

	    private string HandleNestedTypeProperty(int pad, PropertyInfo prop, string schema, IEnumerable<PropertyInfo> props, IEnumerable<CustomAttributeData> customAttributes)
	    {
	        var nestedType = GetRecursively(prop.PropertyType, pad + 2, customAttributes);
            if(nestedType == null)
                return string.Empty;

	        if (!string.IsNullOrWhiteSpace(nestedType))
	        {
                var name = GetPropertyName(prop);
	            schema += ("\"" + name + "\":").Indent(pad) + Environment.NewLine;
	            schema += nestedType;
	        }
	        else
	        {
	            // is it one of ?
	            var subclasses =prop.PropertyType.Assembly.GetTypes()
                    .Where(type => type.IsSubclassOf(prop.PropertyType))
                    .ToArray();

	            if (!subclasses.Any())
	                return string.Empty;

                schema += GetOneOfProperty(prop, subclasses, pad);
	        }

	        if (prop != props.Last() && !String.IsNullOrWhiteSpace(schema) && schema.Length > "\r\n".Length)
	            schema = schema.Substring(0, schema.Length - "\r\n".Length) + ",\r\n";

	        return schema;
	    }

	    private string GetOneOfProperty(PropertyInfo prop, IEnumerable<Type> subclasses, int pad)
	    {
            var name = GetPropertyName(prop);
	        var oneOf = ("\"" + name + "\": {").Indent(pad) + Environment.NewLine;
	        oneOf += "\"type\": \"object\",".Indent(pad + 2) + Environment.NewLine;
	        oneOf += "\"oneOf\": [".Indent(pad + 2) + Environment.NewLine;
	        foreach (var subclass in subclasses.Distinct())
	        {
	            var className = GetClassName(subclass);
	            oneOf += ("{ \"$ref\": \"#/definitions/" + className + "\" },").Indent(pad + 4) + Environment.NewLine;
                if (!definitions.ContainsKey(className)) { 
                    definitions.Add(className, subclass);
}
	        }
	        oneOf = oneOf.Substring(0, oneOf.Length - Environment.NewLine.Length - 1) + Environment.NewLine;
	        oneOf += "]".Indent(pad + 2) + Environment.NewLine;
	        oneOf += "}".Indent(pad) + Environment.NewLine;
	        return oneOf;
	    }

	    private static string BuildProperty(PropertyInfo prop, IEnumerable<CustomAttributeData> customAttributes, int pad)
		{
	        var res = BuildPropertyCommon(prop, customAttributes);
	        res += "},\r\n";

			res = res.Indent(pad);
			return res;
		}

        private static string BuildLastProperty(PropertyInfo prop, IEnumerable<CustomAttributeData> customAttributes, int pad)
        {
            var res = BuildPropertyCommon(prop, customAttributes);

            res += "}\r\n";

            res = res.Indent(pad);
            return res;
        }

	    private static string BuildPropertyCommon(PropertyInfo prop, IEnumerable<CustomAttributeData> customAttributes)
	    {
	        var name = GetPropertyName(prop);

	        var res = "\"" + name + "\": { \"type\": " + SchemaTypeMapper.GetAttribute(prop.PropertyType);

	        res += HandleRequiredAttribute(prop, customAttributes);

	        res += HandleValidationAttributes(customAttributes);
	        return res;
	    }

        private static string HandleRequiredAttribute(IEnumerable<CustomAttributeData> customAttributes)
        {
            var res = string.Empty;

            if (customAttributes.Any(a => a.AttributeType == typeof(RequiredAttribute)))
            {
                res += "\"required\": true";
            }
            return res;
        }

	    private static string HandleRequiredAttribute(PropertyInfo prop, IEnumerable<CustomAttributeData> customAttributes)
	    {
	        var res = string.Empty;

	        if (customAttributes.Any(a => a.AttributeType == typeof (RequiredAttribute)))
	        {
	            res += ", \"required\": true";
	        }
	        else if (IsNullable(prop.PropertyType))
	        {
	            res += ", \"required\": false";
	        }
	        return res;
	    }

	    private static string HandleValidationAttributes(IEnumerable<CustomAttributeData> customAttributes)
	    {
	        return customAttributes.Where(p => p.AttributeType != typeof (RequiredAttribute)).Aggregate(string.Empty, (current, p) => current + HandleValidationAttribute(p));
	    }

	    private static string HandleValidationAttribute(CustomAttributeData attribute)
	    {
	        string res = string.Empty;

	        switch (attribute.AttributeType.Name)
	        {
	            case "MaxLengthAttribute":
                    res += ", \"maxLength\": " + attribute.ConstructorArguments.First().Value;
	                break;
	            case "MinLengthAttribute":
                    res += ", \"minLength\": " + attribute.ConstructorArguments.First().Value;
	                break;
	            case "RangeAttribute":
                    if(!IsMinValue(attribute.ConstructorArguments.First()))
                        res += ", \"minimum\": " + Format(attribute.ConstructorArguments.First());
                    if (!IsMaxValue(attribute.ConstructorArguments.Last()))
                        res += ", \"maximum\": " + Format(attribute.ConstructorArguments.Last());
	                break;
                case "EmailAddressAttribute":
	                res += @", ""pattern"": ""[^\\s@]+@[^\\s@]+\\.[^\\s@]""";
                    break;
                case "UrlAttribute":
                    res += @", ""pattern"": ""^(ftp|http|https):\/\/[^ \""]+$""";
                    break;
                //case "RegularExpressionAttribute":
                //    res += ", \"pattern\": " + "\"" + attribute.ConstructorArguments.First().Value + "\"";
                //    break;
	        }
	        return res;
	    }

	    private static bool IsMaxValue(CustomAttributeTypedArgument argument)
	    {
	        if (argument.ArgumentType == typeof (int))
	            return int.MaxValue == (int) argument.Value;

	        return Math.Abs(double.MaxValue - (double) argument.Value) < 1;
	    }

        private static bool IsMinValue(CustomAttributeTypedArgument argument)
	    {
            if (argument.ArgumentType == typeof(int))
                return int.MinValue == (int)argument.Value;

            return Math.Abs(double.MinValue - (double)argument.Value) < 1;
	    }

	    private static object Format(CustomAttributeTypedArgument argument)
	    {
            var us = new CultureInfo("en-US");
	        return argument.ArgumentType == typeof (int)
	            ? argument.Value
	            : Convert.ToDouble(argument.Value).ToString("F", us);
	    }

	    private static string GetPropertyName(MemberInfo property)
	    {
            return GetMemberNameByType(property, typeof(JsonPropertyAttribute));
	    }

        private static string GetClassName(MemberInfo @class)
        {
            return GetMemberNameByType(@class, typeof(JsonObjectAttribute));
        }

        private static string GetMemberNameByType(MemberInfo @class, Type attributeType)
        {
            var className = @class.Name;
            if (@class.CustomAttributes.All(a => a.AttributeType != attributeType))
                return className;

            var attr = @class.CustomAttributes.First(a => a.AttributeType == attributeType);
            if (attr.ConstructorArguments.Any())
                className = attr.ConstructorArguments.First().Value.ToString();

            return className;
        }

		public static bool IsNullable(Type t)
		{
			return Nullable.GetUnderlyingType(t) != null;
		}

	    private static bool IsArrayOrEnumerable(Type type)
	    {
	        return type.IsArray || (type.IsGenericType &&
	                                (type.GetGenericTypeDefinition() == typeof (IEnumerable<>)
	                                 || type.GetGenericTypeDefinition() == typeof (ICollection<>)
	                                 || type.GetGenericTypeDefinition() == typeof (Collection<>)
	                                 || type.GetGenericTypeDefinition() == typeof (IList<>)
	                                 || type.GetGenericTypeDefinition() == typeof (List<>)
	                                    )
	            );
	    }

	}
}