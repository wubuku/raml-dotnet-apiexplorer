using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Linq;
using Raml.Parser.Expressions;

namespace RAML.WebApiExplorer
{
    public class Raml1TypeBuilder
    {
        private readonly RamlTypesOrderedDictionary raml1Types;
        private readonly ICollection<Type> Types = new Collection<Type>();

        public Raml1TypeBuilder(RamlTypesOrderedDictionary raml1Types)
        {
            this.raml1Types = raml1Types;
        }

        public string Add(Type type)
        {
            var typeName = GetTypeName(type);

            if(Types.Contains(type))
                return typeName;

            Types.Add(type);

            RamlType raml1Type;

            if (type.IsGenericType && TypeBuilderHelper.IsGenericWebResult(type))
                type = type.GetGenericArguments()[0];

            if (TypeBuilderHelper.IsArrayOrEnumerable(type))
            {
                var elementType = GetElementType(type);
                if (TypeBuilderHelper.IsArrayOrEnumerable(elementType))
                {
                    var subElementType = GetElementType(elementType);
                    if (!HasPropertiesOrParentType(subElementType))
                        return string.Empty;

                    raml1Type = GetArrayOfArray(subElementType);
                }
                else
                {
                    if (!HasPropertiesOrParentType(elementType))
                        return string.Empty;

                    raml1Type = GetArray(elementType);                    
                }
            }
            else if (IsDictionary(type))
            {
                raml1Type = GetMap(type);
            }
            else if (type.IsEnum)
            {
                raml1Type = GetEnum(type);
            }
            else if (Raml1TypeMapper.Map(type) != null)
            {
                raml1Type = GetScalar(type);
            }
            else
            {
                if (!HasPropertiesOrParentType(type))
                    return string.Empty;

                raml1Type = GetObject(type);
            }

            if(raml1Type != null)
                AddType(type, raml1Type);

            return typeName;
        }

        private RamlType GetMap(Type type)
        {
            var subtype = type.GetGenericArguments()[1];
            var subtypeName = GetTypeName(subtype);

            if(Raml1TypeMapper.Map(subtype) == null)
                subtypeName = Add(subtype);

            if(string.IsNullOrWhiteSpace(subtypeName))
                return null;

            var raml1Type = new RamlType
            {
                Object = new ObjectType
                {
                    Properties = new Dictionary<string, RamlType>()
                    {
                        {
                            "[]", new RamlType
                            {
                                Type = subtypeName
                            }
                        }
                    }
                }
            };

            return raml1Type;
        }

        private RamlType GetArrayOfArray(Type subElementType)
        {
            string elementTypeName;
            if (Raml1TypeMapper.Map(subElementType) == null)
                elementTypeName = Add(subElementType);
            else
                elementTypeName = Raml1TypeMapper.Map(subElementType);

            if (string.IsNullOrWhiteSpace(elementTypeName))
                return null;

            var raml1Type = new RamlType
            {
                Array = new ArrayType
                {
                    Items = new RamlType
                    {
                        Array = new ArrayType
                        {
                            Items = new RamlType
                            {
                                Name = GetTypeName(subElementType),
                                Type = elementTypeName
                            }
                        }
                    }
                }
            };

            return raml1Type;
        }

        private RamlType GetArray(Type elementType)
        {
            string elementTypeName;
            if (Raml1TypeMapper.Map(elementType) == null)
                elementTypeName = Add(elementType);
            else
                elementTypeName = Raml1TypeMapper.Map(elementType);
                
            
            if (string.IsNullOrWhiteSpace(elementTypeName))
                return null;

            return new RamlType
            {
                Array = new ArrayType
                {
                    Items = new RamlType
                    {
                        Name = GetTypeName(elementType),
                        Type = elementTypeName
                    }
                }
            };
        }

        private RamlType GetScalar(Type type)
        {
            var ramlType = new RamlType
            {
                Scalar = new Property
                {
                    Type = Raml1TypeMapper.Map(type),
                }
            };
            return ramlType;
        }

        private RamlType GetEnum(Type type)
        {
            var ramlType = new RamlType
            {
                Type = "string",
                Scalar = new Property { Enum = type.GetEnumNames() } // TODO: check!!
            };

            return ramlType;
        }

        private RamlType GetObject(Type type)
        {
            var raml1Type = new RamlType();
            raml1Type.Object = new ObjectType();
            raml1Type.Type = "object";

            if (type.BaseType != null && type.BaseType != typeof(object))
            {
                var parent = GetObject(type.BaseType);
                AddType(type.BaseType, parent);
                raml1Type.Type = GetTypeName(type.BaseType);
            }

            if (type.GetProperties().Count(p => p.CanWrite) > 0)
            {
                raml1Type.Object.Properties = GetProperties(type);
            }
            return raml1Type;
        }

        private IDictionary<string, RamlType> GetProperties(Type type)
        {
            var props = type.GetProperties().Where(p => p.CanWrite).ToArray();
            var dic = new Dictionary<string, RamlType>();
            foreach (var prop in props)
            {
                var key = GetPropertyName(prop);
                var ramlType = GetProperty(prop);
                if(ramlType != null)
                    dic.Add(key, ramlType);
            }
            return dic;
        }

        private string GetPropertyName(PropertyInfo prop)
        {
            return prop.Name + (IsOptionalProperty(prop, prop.CustomAttributes) ? "?" : "");
        }

        private RamlType GetProperty(PropertyInfo prop)
        {
            if (prop.PropertyType.IsEnum)
                return GetEnum(prop.PropertyType);
            
            if (Raml1TypeMapper.Map(prop.PropertyType) != null)
                return HandlePrimitiveTypeProperty(prop);

            if (TypeBuilderHelper.IsArrayOrEnumerable(prop.PropertyType))
                return GetArray(prop.PropertyType);
            
            if (IsDictionary(prop.PropertyType))
                return GetMap(prop.PropertyType);
            
            return HandleNestedTypeProperty(prop);
        }

        private RamlType HandlePrimitiveTypeProperty(PropertyInfo prop)
        {
            var ramlTypeProp = GetScalar(prop.PropertyType);
            HandleValidationAttributes(ramlTypeProp, prop.CustomAttributes);
            return ramlTypeProp;
        }

        private void HandleValidationAttributes(RamlType ramlTypeProp, IEnumerable<CustomAttributeData> customAttributes)
        {
            foreach (var attribute in customAttributes)
            {
                HandleValidationAttribute(ramlTypeProp, attribute);
            }
        }

        private static void HandleValidationAttribute(RamlType ramlTypeProp, CustomAttributeData attribute)
        {
            switch (attribute.AttributeType.Name)
            {
                case "MaxLengthAttribute":
                    ramlTypeProp.Scalar.MaxLength = (int?) attribute.ConstructorArguments.First().Value;
                    break;
                case "MinLengthAttribute":
                    ramlTypeProp.Scalar.MinLength = (int?) attribute.ConstructorArguments.First().Value;
                    break;
                case "RangeAttribute":
                    if (!TypeBuilderHelper.IsMinValue(attribute.ConstructorArguments.First()))
                        ramlTypeProp.Scalar.Minimum = ConvertToNullableDecimal(attribute.ConstructorArguments.First().Value);
                    if (!TypeBuilderHelper.IsMaxValue(attribute.ConstructorArguments.Last()))
                        ramlTypeProp.Scalar.Maximum = ConvertToNullableDecimal(attribute.ConstructorArguments.Last().Value);
                    break;
                case "EmailAddressAttribute":
                    ramlTypeProp.Scalar.Pattern = @"pattern: [^\\s@]+@[^\\s@]+\\.[^\\s@]";
                    break;
                case "UrlAttribute":
                    ramlTypeProp.Scalar.Pattern = @"pattern: ^(ftp|http|https):\/\/[^ \""]+$";
                    break;
                //case "RegularExpressionAttribute":
                //    ramlTypeProp.Scalar.Pattern = "pattern: " + attribute.ConstructorArguments.First().Value;
                //    break;
            }
        }

        private static decimal? ConvertToNullableDecimal(object value)
        {
            if (value == null)
                return null;

            return Convert.ToDecimal(value);
        }

        private RamlType HandleNestedTypeProperty(PropertyInfo prop)
        {
            var typeName = Add(prop.PropertyType);

            if (string.IsNullOrWhiteSpace(typeName))
                return null;

            return new RamlType { Type = typeName };
        }

        private static bool IsOptionalProperty(PropertyInfo prop, IEnumerable<CustomAttributeData> customAttributes)
        {
            return customAttributes.All(a => a.AttributeType != typeof(RequiredAttribute)) && TypeBuilderHelper.IsNullable(prop.PropertyType);
        }

        private string HandleValidationAttribute(CustomAttributeData attribute)
        {
            var res = string.Empty;

            switch (attribute.AttributeType.Name)
            {
                case "MaxLengthAttribute":
                    res += Environment.NewLine + "maxLength: " + attribute.ConstructorArguments.First().Value;
                    break;
                case "MinLengthAttribute":
                    res += Environment.NewLine + "minLength: " + attribute.ConstructorArguments.First().Value;
                    break;
                case "RangeAttribute":
                    if (!TypeBuilderHelper.IsMinValue(attribute.ConstructorArguments.First()))
                        res += Environment.NewLine + "minimum: " + TypeBuilderHelper.Format(attribute.ConstructorArguments.First());
                    if (!TypeBuilderHelper.IsMaxValue(attribute.ConstructorArguments.Last()))
                        res += Environment.NewLine + "maximum: " + TypeBuilderHelper.Format(attribute.ConstructorArguments.Last());
                    break;
                case "EmailAddressAttribute":
                    res += Environment.NewLine + @"pattern: [^\\s@]+@[^\\s@]+\\.[^\\s@]";
                    break;
                case "UrlAttribute":
                    res += Environment.NewLine + @"pattern: ^(ftp|http|https):\/\/[^ \""]+$";
                    break;
                //case "RegularExpressionAttribute":
                //    res += "pattern: " + " + attribute.ConstructorArguments.First().Value;
                //    break;
            }
            return res;
        }

        private static Type GetElementType(Type type)
        {
            return type.GetElementType() ?? type.GetGenericArguments()[0];
        }

        private static bool IsDictionary(Type type)
        {
            return type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Dictionary<,>) || type.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        }

        private static bool HasPropertiesOrParentType(Type type)
        {
            return (type.BaseType != null && type.BaseType != typeof(object)) || type.GetProperties().Any(p => p.CanWrite);
        }

        private void AddType(Type type, RamlType raml1Type)
        {
            var typeName = GetTypeName(type);

            // handle case of different types with same class name
            if (raml1Types.ContainsKey(typeName))
                typeName = GetUniqueName(typeName);

            raml1Types.Add(typeName, raml1Type);
        }

        private static string GetTypeName(Type type)
        {
            var typeName = type.Name;
            
            if (IsDictionary(type)) 
                typeName = type.GetGenericArguments()[1].Name + "Map";

            if (TypeBuilderHelper.IsArrayOrEnumerable(type))
                typeName = "ListOf" + GetTypeName(GetElementType(type));

            return typeName;
        }

        private string GetUniqueName(string schemaName)
        {
            for (var i = 0; i < 1000; i++)
            {
                schemaName += i;
                if (!raml1Types.ContainsKey(schemaName))
                    return schemaName;
            }
            throw new InvalidOperationException("Could not find a unique name. You have more than 1000 types with the same class name");
        }


    }
}