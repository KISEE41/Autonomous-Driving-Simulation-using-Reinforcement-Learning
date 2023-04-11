using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.IO;
using System.Net.Sockets;
using System;

//Defininf the datas to be send to the server
//Defining classes for encoding and decoding JSONs
public class StateMessage
{
    public string encodedImage;
}

public class ControlMessage{
    public int action; //A control command in the form of an integer
    public bool resetGame;  //If true, the game will be reset and the action will be disregarded

    public bool quit; //If true, the application is terminated
}

//create events
public class client: MonoBehaviour{
    //variables needed for socket connection
    public string Host = "127.0.0.1";
    public int Port = 1234;

    //communicationg objects
    private TcpClient _mySocket;
    private NetworkStream _theStream;

    //varaibles needed for rendering image
    public int RenderHeight = 100;
    public int RenderWidth = 100;

    //control functions
    public carcontroller controller;
    public void Awake(){
        //Setting up the socket
        SetupSocket();
        Screen.SetResolution(RenderWidth, RenderHeight, false, -1);
        StartCoroutine(SendAndReceive());
    }

    //Function setting up the socket
    private void SetupSocket(){
        try
        {
            //Setting up the socket at port: 1234"
            _mySocket = new TcpClient(Host, Port);
            Debug.Log("Connected to server...");
            
            //setting the stream to receive and send data
            _theStream = _mySocket.GetStream();
            Debug.Log("The stream configuration completed, can send and received data now");
        }
        catch (System.Exception e)
        {
            Debug.Log("Socket Error: " + e);
        }
    }

    //now the sending and receiving part begins
    IEnumerator SendAndReceive(){
        yield return new WaitForEndOfFrame();
        // double start_time = Time.time;
        while (true){
            //python sends a command, which is then invoked
            var read = ReadMessage();
            Debug.Log("Read: " + read);
            var msg = JsonUtility.FromJson<ControlMessage>(read);

            // If the game is being reset, reset it then exit early
            if (msg.resetGame)
            {
                //Reset the game
                Debug.Log("Resetting the Environment...");
                controller.Reset();
                yield return new WaitForSeconds(.5f);
            }
            else
            {
                // Trigger action events here
                if (msg.action == 0){
                    controller.Accelerate();
                }

                else if (msg.action == 3){
                    controller.Deccelerate();
                }

                if (msg.action == 1){
                    controller.turnLeft();
                }

                else if (msg.action == 2){
                    controller.turnRight();
                }

                else {
                    Debug.Log("Invalid action request...");
                }
            }

            if (msg.quit){
                OnApplicationQuit();
                Application.Quit();
            }

            //wait for the new state
            yield return new WaitForEndOfFrame();
            
            //The new state is encoded, along with the game state information
            var encoded = GetFramEncoded();    //Get the rendered camera image


            //preparing for transmission of data
            //object to pass different variable types
            var message = new StateMessage{    
                encodedImage = encoded
            };

            var json = JsonUtility.ToJson(message);

            //Sending data
            WriteMessage(json);
        }
    }

    public void OnApplicationQuit()
    {
        // Send a message to the server that Unity is cutting out
        Debug.Log("Quitting the application...");
        WriteMessage("QUITING!");
    }

    private string GetFramEncoded(){
        //Return the current game Screen encoded in a base64 string
        var width = Screen.width;
        var height = Screen.height;

        var tex = new Texture2D(width, height, TextureFormat.RGB24, false); //creating the texture to save the camera rendered texture
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0); //reading the pixels
        tex.Apply();  // the texture is saved to texture variavble

        var bytes = tex.EncodeToPNG();   //conveting to png bytes
        var encodedImage = Convert.ToBase64String(bytes); //converting to base64 string sothat sting can be converted to bytes for transmission
        Destroy(tex);  //clearing texture for further cycle
        return encodedImage;  //image is string format
    }

    //This function read the data from server
    private string ReadMessage(){
        var msg = "";
        
        while (msg.Length == 0 || msg[msg.Length -1] != '}'){ //only accepts json formatted data 
            msg += (char)_theStream.ReadByte();               //so check until the data is received or the end of data ie }  
        }
        _theStream.Flush();
        return msg;
    }

    private void WriteMessage(string message)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(message);
        try
        {
            _theStream.Write(bytes, 0, bytes.Length);
        }
        catch (NullReferenceException)
        {
            Debug.Log("ERROR! When sending: " + message);
            Debug.Log("NetworkStream Value: " + _theStream);
        }

        _theStream.Flush();
    }

    private void OnDisable(){
        Debug.Log("Disconnecting the network and socket...........");
        WriteMessage("QUITING!");
        _theStream.Close();
        _mySocket.Close();
    }
}
