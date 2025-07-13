module Backend.Main exposing
    ( State
    , webServiceMain
    )

import Bytes
import Bytes.Encode
import Platform.WebService


type alias State =
    { posixTimeMilli : Int
    , httpRequestsToRespondTo : List Platform.WebService.HttpRequestEventStruct
    }


webServiceMain : Platform.WebService.WebServiceConfig State
webServiceMain =
    { init = ( initState, [] )
    , subscriptions = subscriptions
    }


subscriptions : State -> Platform.WebService.Subscriptions State
subscriptions state =
    let
        nextCompletionPosixTimeMilli =
            state
                |> getHttpRequestsWithCompletionTimes
                |> List.map (Tuple.second >> .completionPosixTimeMilli)
                |> List.minimum
    in
    { httpRequest = updateForHttpRequestEvent
    , posixTimeIsPast =
        Just
            { minimumPosixTimeMilli = nextCompletionPosixTimeMilli |> Maybe.withDefault (state.posixTimeMilli + 1000)
            , update =
                \{ currentPosixTimeMilli } stateBefore ->
                    { stateBefore | posixTimeMilli = currentPosixTimeMilli } |> updateForHttpResponses
            }
    }


updateForHttpRequestEvent : Platform.WebService.HttpRequestEventStruct -> State -> ( State, Platform.WebService.Commands State )
updateForHttpRequestEvent httpRequestEvent stateBefore =
    let
        state =
            { stateBefore
                | posixTimeMilli = httpRequestEvent.posixTimeMilli
                , httpRequestsToRespondTo = httpRequestEvent :: stateBefore.httpRequestsToRespondTo
            }
    in
    updateForHttpResponses state


updateForHttpResponses : State -> ( State, Platform.WebService.Commands State )
updateForHttpResponses stateBefore =
    let
        httpResponses : List Platform.WebService.RespondToHttpRequestStruct
        httpResponses =
            stateBefore
                |> getHttpRequestsWithCompletionTimes
                |> List.filter
                    (\( _, { completionPosixTimeMilli } ) ->
                        completionPosixTimeMilli <= stateBefore.posixTimeMilli
                    )
                |> List.map
                    (\( requestEvent, _ ) ->
                        let
                            ageInMilliseconds : Int
                            ageInMilliseconds =
                                stateBefore.posixTimeMilli - requestEvent.posixTimeMilli
                        in
                        { httpRequestId = requestEvent.httpRequestId
                        , response =
                            { statusCode = 200
                            , body =
                                ("Completed in " ++ (ageInMilliseconds |> String.fromInt) ++ " milliseconds.")
                                    |> encodeStringToBytes
                                    |> Just
                            , headersToAdd = []
                            }
                        }
                    )

        state : State
        state =
            { stateBefore
                | httpRequestsToRespondTo =
                    stateBefore.httpRequestsToRespondTo
                        |> List.filter
                            (\requestEvent ->
                                not
                                    (httpResponses
                                        |> List.any (.httpRequestId >> (==) requestEvent.httpRequestId)
                                    )
                            )
            }
    in
    ( state
    , List.map
        Platform.WebService.RespondToHttpRequest
        httpResponses
    )


getHttpRequestsWithCompletionTimes :
    State
    -> List ( Platform.WebService.HttpRequestEventStruct, { completionPosixTimeMilli : Int } )
getHttpRequestsWithCompletionTimes state =
    state.httpRequestsToRespondTo
        |> List.map (\requestEvent -> ( requestEvent, completionTimeForHttpRequest requestEvent ))


completionTimeForHttpRequest : Platform.WebService.HttpRequestEventStruct -> { completionPosixTimeMilli : Int }
completionTimeForHttpRequest httpRequest =
    let
        delayMilliseconds =
            httpRequest.request.headers
                |> List.filter (.name >> (==) "delay-milliseconds")
                |> List.filterMap (.values >> List.head)
                |> List.head
                |> Maybe.andThen String.toInt
                |> Maybe.withDefault 0
    in
    { completionPosixTimeMilli = delayMilliseconds + httpRequest.posixTimeMilli }


encodeStringToBytes : String -> Bytes.Bytes
encodeStringToBytes =
    Bytes.Encode.string >> Bytes.Encode.encode


initState : State
initState =
    { posixTimeMilli = 0, httpRequestsToRespondTo = [] }
