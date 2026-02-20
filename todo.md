- hvordan fungerer scaffolding / code first med nuværende pg backplane?

- er serverid på sseconnection redundant?

- what is the backplane even doing with this new EF centric approach?

- is it possible to make a client id-less system?

- ws/sse conn for hvert endpoint?
  - det vil nok give meget bøvl 
    - det giver et mere endpoint baseret layout
    - ingen connection må begynde at blive stateful, for der skal vedligeholdes flere
    - problem: hver connection vil være et nyt connection ID og det duer ikke
    - vent lige: hvad med at hver connection repræsenterer queryen og ikke browseren?
      - hvordan vil man taget en given klient når hver browser har X conncetions? targeter man ud fra den åbne forbindelse til den konkrete feature?
- re-branding til EF.Core.Realtime?
