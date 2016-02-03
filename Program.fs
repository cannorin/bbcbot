open System
open System.IO
open System.Runtime.Serialization
open System.Xml
open System.Threading
open System.Threading.Tasks
open System.Drawing
open System.Drawing.Imaging
open System.Net
open System.Text.RegularExpressions
open System.Collections.Generic
open FSharp.Data
open CoreTweet

module Main =
  begin

    let getTokens () = 
      let x = new DataContractSerializer(typeof<Tokens>) in

      if(File.Exists("bot.xml")) then
        use y = XmlReader.Create("bot.xml") in
            x.ReadObject(y) :?> Tokens
      else
        let se = OAuth.Authorize("pi6s0ciykBLb7ESM2KXZGTlye", "0pFiuE1bdhEwP7Ze2LpbfDNY22xODTThQnzgfBQpzrsikUGL1C") in
        Console.WriteLine(se.AuthorizeUri);
        Console.Write("pin> ");
        let g = se.GetTokens(Console.ReadLine()) in
        let s = XmlWriterSettings() in
        s.Encoding <- System.Text.UTF8Encoding(false);
        use y = XmlWriter.Create("bot.xml", s) in
            x.WriteObject(y, g)
        g

    let (|Regex|_|) pattern input =
      let m = Regex.Match(input, pattern)
      if m.Success then Some(List.tail [ for g in m.Groups -> g.Value ])
      else None

    let readprogramme (d : HtmlDocument) =
      let pdiv = d.Descendants ["div"] |> Seq.find (fun x -> x.HasClass "local-content") in
      let title = 
        pdiv.Descendants ["span"] 
        |> Seq.find (fun x -> x.HasClass "title-container") 
        |> (fun x -> x.InnerText ()) in
      let img =
        pdiv.Descendants ["h2"]
        |> Seq.find (fun x -> x.HasClass "png_bg")
        |> (fun x -> x.AttributeValue "style")
        |> function
          | Regex "background-image: url\((.+)\)" [a] -> a
          | _ -> failwith "Image not found" in
      (title, img)

    let readtracks (d : HtmlDocument) =
      d.Descendants ["li"]
        |> Seq.filter (fun x -> x.HasClass "track")
        |> Seq.map (fun x -> 
        (
          x.Descendants ["h3"] |> Seq.head |> (fun x -> x.InnerText ()),
          x.Descendants ["h4"] |> Seq.head |> (fun x -> x.InnerText ()),
          x.Descendants ["img"] |> Seq.head |> (fun x -> x.AttributeValue "src")
        ))

    [<EntryPoint>]
    let main argv =
      let t = getTokens () in
      let mhash = HashSet<string> (StringComparer.Ordinal) in
      let rp = ref "" in
      let rec loop () =
        try
          let d = HtmlDocument.Load("http://www.bbc.co.uk/radio1") in
          let ms = readtracks d in
          let (pt, pimg) = readprogramme d in
          let (mt, ma, mimg) = Seq.head ms in

          if pt <> !rp then
            let req = WebRequest.Create pimg in
            use str = req.GetResponse().GetResponseStream() in
              let id = t.Media.Upload str in
              t.Statuses.Update(status = (sprintf "Programme: %s #BBCRadio1" pt), media_ids = [id.MediaId]) |> ignore;
            rp := pt;
            mhash.Clear()

          if not (mhash.Contains mt) then
            try
              let req = WebRequest.Create mimg in
              use str = req.GetResponse().GetResponseStream() in
                let id = t.Media.Upload str in
                t.Statuses.Update(status = (sprintf "%s - %s #BBCRadio1" mt ma), media_ids = [id.MediaId]) |> ignore;
            with
              | _ -> t.Statuses.Update(status = (sprintf "%s - %s #BBCRadio1" mt ma)) |> ignore; 
            
            for (m, _, _) in ms do
              mhash.Add m |> ignore

        with
          | e -> t.DirectMessages.New("cannorin", e.ToString()) |> ignore
        
        Thread.Sleep 30000;
        loop()
      
      in loop ();
      0

  end