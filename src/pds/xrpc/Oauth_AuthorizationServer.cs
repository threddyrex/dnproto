using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;

namespace dnproto.pds.xrpc;


public class Oauth_AuthorizationServer : BaseXrpcCommand
{
    public IResult GetResponse()
    {
        return Results.Json(new 
        {
            issuer = $"https://{Pds.Config.PdsHostname}",
            request_parameter_supported = true,
            request_uri_parameter_supported = true,
            require_request_uri_registration = true,
            scopes_supported = new JsonArray(){"atproto","transition:email","transition:generic","transition:chat.bsky"},
            subject_types_supported = new JsonArray(){"public"},
            response_types_supported = new JsonArray(){"code"},
            response_modes_supported = new JsonArray(){"query","fragment","form_post"},
            grant_types_supported = new JsonArray(){"authorization_code","refresh_token"},
            code_challenge_methods_supported = new JsonArray(){"S256"},
            ui_locales_supported = new JsonArray(){"en-US"},
            display_values_supported = new JsonArray(){"page","popup","touch"},
            request_object_signing_alg_values_supported = new JsonArray(){"RS256","RS384","RS512","PS256","PS384","PS512","ES256","ES256K","ES384","ES512","none"},
            authorization_response_iss_parameter_supported = true,
            request_object_encryption_alg_values_supported = new JsonArray(){},
            request_object_encryption_enc_values_supported = new JsonArray(){},
            jwks_uri = $"https://{Pds.Config.PdsHostname}/oauth/jwks",
            authorization_endpoint = $"https://{Pds.Config.PdsHostname}/oauth/authorize",
            token_endpoint = $"https://{Pds.Config.PdsHostname}/oauth/token",
            token_endpoint_auth_methods_supported = new JsonArray(){"none","private_key_jwt"},
            token_endpoint_auth_signing_alg_values_supported = new JsonArray(){ "RS256","RS384","RS512","PS256","PS384","PS512","ES256","ES256K","ES384","ES512"},
            revocation_endpoint = $"https://{Pds.Config.PdsHostname}/oauth/revoke",
            pushed_authorization_request_endpoint = $"https://{Pds.Config.PdsHostname}/oauth/par",
            require_pushed_authorization_requests = true,
            dpop_signing_alg_values_supported = new JsonArray(){"RS256","RS384","RS512","PS256","PS384","PS512","ES256","ES256K","ES384","ES512"},
            protected_resources = new JsonArray(){$"https://{Pds.Config.PdsHostname}"},
            client_id_metadata_document_supported = true,
            prompt_values_supported = new JsonArray(){"none","login","consent","select_account","create"}
        },
        statusCode: 200);
    }
}