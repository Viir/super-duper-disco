module Backend.Main exposing
    ( State
    , webServiceMain
    )

import Bytes
import Bytes.Decode
import Bytes.Encode
import Platform.WebService


type alias State =
    String


webServiceMain : Platform.WebService.WebServiceConfig State
webServiceMain =
    { init = ( "", [] )
    , subscriptions = subscriptions
    }


subscriptions : State -> Platform.WebService.Subscriptions State
subscriptions _ =
    { httpRequest = updateForHttpRequestEvent
    , posixTimeIsPast = Nothing
    }


updateForHttpRequestEvent : Platform.WebService.HttpRequestEventStruct -> State -> ( State, Platform.WebService.Commands State )
updateForHttpRequestEvent httpRequestEvent stateBefore =
    let
        state =
            case httpRequestEvent.request.method |> String.toLower of
                "get" ->
                    stateBefore

                "post" ->
                    let
                        addition =
                            httpRequestEvent.request.body
                                |> Maybe.map (decodeBytesToString >> Maybe.withDefault "Failed to decode bytes to string")
                                |> Maybe.withDefault ""
                    in
                    stateBefore ++ addition

                _ ->
                    stateBefore

        httpResponse : Platform.WebService.RespondToHttpRequestStruct
        httpResponse =
            { httpRequestId = httpRequestEvent.httpRequestId
            , response =
                { statusCode = 200
                , body =
                    state
                        |> Bytes.Encode.string
                        |> Bytes.Encode.encode
                        |> Just
                , headersToAdd = []
                }
            }
    in
    ( state
    , [ Platform.WebService.RespondToHttpRequest httpResponse ]
    )


decodeBytesToString : Bytes.Bytes -> Maybe String
decodeBytesToString bytes =
    bytes |> Bytes.Decode.decode (Bytes.Decode.string (bytes |> Bytes.width))
