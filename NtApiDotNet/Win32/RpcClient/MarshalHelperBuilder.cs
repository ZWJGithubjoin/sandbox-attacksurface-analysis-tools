﻿//  Copyright 2019 Google Inc. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.


using NtApiDotNet.Ndr;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NtApiDotNet.Win32.RpcClient
{
    internal class MarshalHelperBuilder
    {
        private int _current_unmarshal_id;
        private int _current_marshal_id;

        public CodeExpression CastUnmarshal(CodeExpression expr)
        {
            return new CodeCastExpression(UnmarshalHelperType, expr);
        }

        public CodeExpression CastMarshal(CodeExpression expr)
        {
            return new CodeCastExpression(MarshalHelperType, expr);
        }

        public CodeTypeDeclaration MarshalHelper { get; }
        public CodeTypeDeclaration UnmarshalHelper { get; }
        public CodeTypeReference MarshalHelperType { get;}
        public CodeTypeReference UnmarshalHelperType { get; }

        public static CodeTypeDeclaration CreateUnmarshalHelperType(CodeNamespace ns, string name)
        {
            var unmarshal_type = new CodeTypeReference(typeof(NdrUnmarshalBuffer));
            var type = ns.AddType(name);
            type.TypeAttributes = TypeAttributes.NestedAssembly;
            type.BaseTypes.Add(unmarshal_type);
            var con = type.AddConstructor(MemberAttributes.Public);
            con.AddParam(unmarshal_type, "u");
            con.BaseConstructorArgs.Add(CodeGenUtils.GetVariable("u"));
            return type;
        }

        public static CodeTypeDeclaration CreateMarshalHelperType(CodeNamespace ns, string name)
        {
            var marshal_type = new CodeTypeReference(typeof(NdrMarshalBuffer));
            var type = ns.AddType(name);
            type.TypeAttributes = TypeAttributes.NestedAssembly;
            type.BaseTypes.Add(marshal_type);
            return type;
        }

        public MarshalHelperBuilder(CodeNamespace ns, string marshal_name, string unmarshal_name)
        {
            MarshalHelper = CreateMarshalHelperType(ns, marshal_name);
            MarshalHelper.AddStartRegion("Marshal Helpers");
            MarshalHelperType = new CodeTypeReference(MarshalHelper.Name);
            UnmarshalHelper = CreateUnmarshalHelperType(ns, unmarshal_name);
            UnmarshalHelper.AddEndRegion();
            UnmarshalHelperType = new CodeTypeReference(UnmarshalHelper.Name);
        }

        private static CodeExpression AddParam(CodeTypeReference type, int arg_count, CodeMemberMethod method)
        {
            string p_name = $"p{arg_count}";
            method.AddParam(type, p_name);
            return CodeGenUtils.GetVariable(p_name);
        }

        private static CodeTypeReference GetBaseType(CodeTypeReference type)
        {
            return type.ArrayElementType ?? type;
        }

        private static CodeMemberMethod AddMethod(CodeTypeDeclaration marshal_type, string method_name, CodeTypeReference generic_type, CodeTypeReference return_type, 
            string name, CodeTypeReference[] pre_args, AdditionalArguments additional_args)
        {
            var method = marshal_type.AddMethod(method_name, MemberAttributes.Public | MemberAttributes.Final);
            method.ReturnType = return_type;
            int arg_count = 0;

            List<CodeExpression> arg_names = new List<CodeExpression>(pre_args.Select(a => AddParam(a, arg_count++, method)));
            arg_names.AddRange(additional_args.FixedArgs);
            arg_names.AddRange(additional_args.Params.Select(a => AddParam(a, arg_count++, method)));

            CodeMethodReferenceExpression generic_method = generic_type != null ? new CodeMethodReferenceExpression(null, name, generic_type) : new CodeMethodReferenceExpression(null, name);
            var invoke_method = new CodeMethodInvokeExpression(generic_method, arg_names.ToArray());
            if (return_type != null)
            {
                method.AddReturn(invoke_method);
            }
            else
            {
                method.Statements.Add(invoke_method);
            }

            return method;
        }

        public string AddGenericUnmarshal(CodeTypeReference type, string name, AdditionalArguments additional_args)
        {
            return AddMethod(UnmarshalHelper, $"Read_{_current_unmarshal_id++}", additional_args.Generic ? GetBaseType(type) : null, type, name, new CodeTypeReference[0], additional_args).Name;
        }

        public string AddGenericUnmarshal(string type_name, string name, AdditionalArguments additional_args)
        {
            return AddGenericUnmarshal(new CodeTypeReference(CodeGenUtils.MakeIdentifier(type_name)), name, additional_args);
        }

        public string AddGenericMarshal(CodeTypeReference type, string name, AdditionalArguments additional_args)
        {
            return AddMethod(MarshalHelper, $"Write_{_current_marshal_id++}", additional_args.Generic ? GetBaseType(type) : null, null, name, new[] { type }, additional_args).Name;
        }

        public string AddGenericMarshal(string type_name, string name, AdditionalArguments additional_args)
        {
            return AddGenericMarshal(new CodeTypeReference(CodeGenUtils.MakeIdentifier(type_name)), name, additional_args);
        }
    }
}