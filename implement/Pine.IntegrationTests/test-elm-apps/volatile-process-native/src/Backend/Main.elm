module Backend.Main exposing
    ( State
    , webServiceMain
    )

import Bytes
import Bytes.Decode
import Bytes.Encode
import Platform.WebService


type alias State =
    { timeMilli : Int
    , runtimeInformation : Maybe Platform.WebService.RuntimeInformationRecord
    , volatileProcessId : Maybe String
    , pendingHttpRequest : Maybe ( { timeMilli : Int }, Platform.WebService.HttpRequestEventStruct )
    }


volatileProcessExecutableFileFromOsPlatform : List ( String, Platform.WebService.LoadDependencyStruct )
volatileProcessExecutableFileFromOsPlatform =
    [ ( "WINDOWS"
      , { hashSha256Base16 = "3a30fcae96bb305187bf2fb7064fd465d0283d160e346bf3b47e24ecefd80752"
        , hintUrls =
            [ "https://github.com/pine-vm/pine/releases/download/native-tools-2023-06-30/native-tools-bin-74c0bbc4fb8ea4e3511a080e4e0fd5541793ef2c-win10-x64.zip" ]
        }
      )
    , ( "LINUX"
      , { hashSha256Base16 = "7367c3ba48cbc0911f6e3e9682964324ca977a47fe7863265f47e99fdfb0b29f"
        , hintUrls =
            [ "https://github.com/pine-vm/pine/releases/download/native-tools-2023-06-30/native-tools-bin-74c0bbc4fb8ea4e3511a080e4e0fd5541793ef2c-linux-x64.zip" ]
        }
      )
    , ( "OSX"
      , { hashSha256Base16 = "5e51e7229278a328ecc3c2b81f2fc70d2e3d250230c14f4d42feb75ae522d335"
        , hintUrls =
            [ "https://github.com/pine-vm/pine/releases/download/native-tools-2023-06-30/native-tools-bin-74c0bbc4fb8ea4e3511a080e4e0fd5541793ef2c-osx-x64.zip" ]
        }
      )
    ]


webServiceMain : Platform.WebService.WebServiceConfig State
webServiceMain =
    { init =
        ( { timeMilli = 0
          , runtimeInformation = Nothing
          , volatileProcessId = Nothing
          , pendingHttpRequest = Nothing
          }
        , []
        )
    , subscriptions = subscriptions
    }


subscriptions : State -> Platform.WebService.Subscriptions State
subscriptions state =
    { httpRequest = updateForHttpRequestEvent
    , posixTimeIsPast =
        Just
            { minimumPosixTimeMilli = state.timeMilli + 400
            , update = updateForCurrentPosixTimeMilli
            }
    }


updateForCurrentPosixTimeMilli : { currentPosixTimeMilli : Int } -> State -> ( State, Platform.WebService.Commands State )
updateForCurrentPosixTimeMilli { currentPosixTimeMilli } stateBefore =
    let
        state : State
        state =
            { stateBefore | timeMilli = currentPosixTimeMilli }
    in
    case stateBefore.pendingHttpRequest of
        Nothing ->
            ( state
            , []
            )

        Just ( pendingHttpRequestContext, pendingHttpRequest ) ->
            if
                currentPosixTimeMilli
                    < pendingHttpRequestContext.timeMilli
                    {-
                       The delay gradually increased due to sporadic failures of tests.
                       TODO: Explore how to get the response earlier.
                    -}
                    + 8000
            then
                ( state
                , []
                )

            else
                ( { state | pendingHttpRequest = Nothing }
                , case stateBefore.volatileProcessId of
                    Nothing ->
                        [ httpResponseCommandInternalServerError pendingHttpRequest
                            "Volatile process disappeared"
                        ]

                    Just volatileProcessId ->
                        [ Platform.WebService.ReadAllFromVolatileProcessNativeCommand
                            { processId = volatileProcessId
                            , update =
                                updateForReadFromVolatileProcess pendingHttpRequest
                            }
                        ]
                )


updateForHttpRequestEvent :
    Platform.WebService.HttpRequestEventStruct
    -> State
    -> ( State, Platform.WebService.Commands State )
updateForHttpRequestEvent httpRequestEvent stateBefore =
    case stateBefore.volatileProcessId of
        Nothing ->
            case stateBefore.runtimeInformation of
                Nothing ->
                    ( stateBefore
                    , [ Platform.WebService.ReadRuntimeInformationCommand
                            (updateForReadRuntimeInformationResponse httpRequestEvent)
                      ]
                    )

                Just runtimeInformation ->
                    createVolatileProcessForRuntimeInformation
                        httpRequestEvent
                        runtimeInformation
                        stateBefore

        Just _ ->
            ( { stateBefore
                | pendingHttpRequest =
                    Just
                        ( { timeMilli = stateBefore.timeMilli }
                        , httpRequestEvent
                        )
              }
            , []
            )


createVolatileProcessForRuntimeInformation :
    Platform.WebService.HttpRequestEventStruct
    -> Platform.WebService.RuntimeInformationRecord
    -> State
    -> ( State, Platform.WebService.Commands State )
createVolatileProcessForRuntimeInformation httpRequestEvent runtimeInformation stateBefore =
    case runtimeInformation.osPlatform of
        Nothing ->
            ( stateBefore
            , [ httpResponseCommandInternalServerError httpRequestEvent
                    "Runtime information contains no OS platform"
              ]
            )

        Just osPlatform ->
            volatileProcessExecutableFileFromOsPlatform
                |> List.filter (Tuple.first >> (==) osPlatform)
                |> List.head
                |> Maybe.map Tuple.second
                |> Maybe.map
                    (\executableFile ->
                        ( stateBefore
                        , [ Platform.WebService.CreateVolatileProcessNativeCommand
                                { request =
                                    { executableFile = executableFile
                                    , arguments = "echo-json"
                                    , environmentVariables = []
                                    }
                                , update = updateForCreateVolatileProcess httpRequestEvent
                                }
                          ]
                        )
                    )
                |> Maybe.withDefault
                    ( stateBefore
                    , [ httpResponseCommandInternalServerError httpRequestEvent
                            ("No executable file found for OS platform '"
                                ++ osPlatform
                                ++ "'"
                            )
                      ]
                    )


updateForReadRuntimeInformationResponse :
    Platform.WebService.HttpRequestEventStruct
    -> Result String Platform.WebService.RuntimeInformationRecord
    -> State
    -> ( State, Platform.WebService.Commands State )
updateForReadRuntimeInformationResponse httpRequestEvent readRuntimeInfoResult stateBefore =
    case stateBefore.volatileProcessId of
        Just volatileProcessId ->
            continueWithVolatileProcess
                { volatileProcessId = volatileProcessId }
                httpRequestEvent
                stateBefore

        Nothing ->
            case readRuntimeInfoResult of
                Err err ->
                    ( stateBefore
                    , [ httpResponseCommandInternalServerError httpRequestEvent
                            ("Failed to read runtime identifier: " ++ err)
                      ]
                    )

                Ok runtimeInformation ->
                    createVolatileProcessForRuntimeInformation
                        httpRequestEvent
                        runtimeInformation
                        stateBefore


updateForCreateVolatileProcess :
    Platform.WebService.HttpRequestEventStruct
    -> Platform.WebService.CreateVolatileProcessResult
    -> State
    -> ( State, Platform.WebService.Commands State )
updateForCreateVolatileProcess httpRequestEvent createVolatileProcessResponse stateBefore =
    case createVolatileProcessResponse of
        Err { exceptionToString } ->
            ( stateBefore
            , [ Platform.WebService.RespondToHttpRequest
                    { httpRequestId = httpRequestEvent.httpRequestId
                    , response = httpResponseInternalServerError ("Error creating volatile process: " ++ exceptionToString)
                    }
              ]
            )

        Ok { processId } ->
            continueWithVolatileProcess
                { volatileProcessId = processId }
                httpRequestEvent
                { stateBefore
                    | volatileProcessId = Just processId
                }


continueWithVolatileProcess :
    { volatileProcessId : String }
    -> Platform.WebService.HttpRequestEventStruct
    -> State
    -> ( State, Platform.WebService.Commands State )
continueWithVolatileProcess { volatileProcessId } httpRequestEvent stateBefore =
    case stateBefore.pendingHttpRequest of
        Just _ ->
            ( stateBefore, [] )

        Nothing ->
            case httpRequestEvent.request.body of
                Nothing ->
                    ( stateBefore
                    , [ httpResponseCommandInternalServerError
                            httpRequestEvent
                            "Missing body in HTTP request"
                      ]
                    )

                Just body ->
                    case Bytes.Decode.decode (Bytes.Decode.string (Bytes.width body)) body of
                        Nothing ->
                            ( stateBefore
                            , [ httpResponseCommandInternalServerError
                                    httpRequestEvent
                                    "Failed to decode body as Base64"
                              ]
                            )

                        Just _ ->
                            ( { stateBefore
                                | pendingHttpRequest =
                                    Just
                                        ( { timeMilli = stateBefore.timeMilli }
                                        , httpRequestEvent
                                        )
                              }
                            , [ Platform.WebService.WriteToVolatileProcessNativeStdInCommand
                                    { processId = volatileProcessId
                                    , stdInBytes = body
                                    , update = updateForWriteToVolatileProcess httpRequestEvent
                                    }
                              ]
                            )


updateForWriteToVolatileProcess :
    Platform.WebService.HttpRequestEventStruct
    -> Result Platform.WebService.RequestToVolatileProcessError ()
    -> State
    -> ( State, Platform.WebService.Commands State )
updateForWriteToVolatileProcess httpRequestEvent writeToVolatileProcessResponse stateBefore =
    let
        continueWithError error =
            ( { stateBefore
                | pendingHttpRequest = Nothing
              }
            , [ httpResponseCommandInternalServerError httpRequestEvent error ]
            )
    in
    case writeToVolatileProcessResponse of
        Err Platform.WebService.ProcessNotFound ->
            continueWithError "Error running in volatile process: ProcessNotFound"

        Err (Platform.WebService.RequestToVolatileProcessOtherError err) ->
            continueWithError ("Error running in volatile process: " ++ err)

        Ok () ->
            ( stateBefore
            , []
            )


updateForReadFromVolatileProcess :
    Platform.WebService.HttpRequestEventStruct
    -> Result Platform.WebService.RequestToVolatileProcessError Platform.WebService.ReadAllFromVolatileProcessNativeSuccessStruct
    -> State
    -> ( State, Platform.WebService.Commands State )
updateForReadFromVolatileProcess pendingHttpRequest readFromProcessResponse stateBefore =
    let
        httpResponse : Platform.WebService.HttpResponse
        httpResponse =
            case readFromProcessResponse of
                Err Platform.WebService.ProcessNotFound ->
                    httpResponseInternalServerError "Error running in volatile process: ProcessNotFound"

                Err (Platform.WebService.RequestToVolatileProcessOtherError err) ->
                    httpResponseInternalServerError ("Error running in volatile process: " ++ err)

                Ok readOk ->
                    case readOk.exitCode of
                        Just processExitCode ->
                            httpResponseInternalServerError
                                ("Process exited unexpected with code " ++ String.fromInt processExitCode)

                        Nothing ->
                            { statusCode = 200
                            , body = Just readOk.stdOutBytes
                            , headersToAdd = []
                            }
    in
    ( stateBefore
    , [ Platform.WebService.RespondToHttpRequest
            { httpRequestId = pendingHttpRequest.httpRequestId
            , response = httpResponse
            }
      ]
    )


httpResponseCommandInternalServerError :
    Platform.WebService.HttpRequestEventStruct
    -> String
    -> Platform.WebService.Command State
httpResponseCommandInternalServerError requestEvent errorMessage =
    Platform.WebService.RespondToHttpRequest
        { httpRequestId = requestEvent.httpRequestId
        , response = httpResponseInternalServerError errorMessage
        }


httpResponseInternalServerError : String -> Platform.WebService.HttpResponse
httpResponseInternalServerError errorMessage =
    { statusCode = 500
    , body = Just (bodyFromString errorMessage)
    , headersToAdd = []
    }


bodyFromString : String -> Bytes.Bytes
bodyFromString =
    Bytes.Encode.string >> Bytes.Encode.encode
