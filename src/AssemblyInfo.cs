//------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
//------------------------------------------------------------

using System.Runtime.CompilerServices;

#if SignAssembly
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2" + AssemblyKeys.MoqPublicKey)]
[assembly: InternalsVisibleTo("Microsoft.Extensions.Caching.Cosmos.Tests" + AssemblyKeys.ProductPublicKey)]
#else
[assembly: InternalsVisibleTo("Microsoft.Extensions.Caching.Cosmos.Tests")]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
#endif