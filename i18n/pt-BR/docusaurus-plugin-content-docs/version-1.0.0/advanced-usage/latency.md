---
sidebar_position: 5
description: De onde vem a latência de input, e quais partes dela você pode remover.
---

# Latência

Três atrasos distintos ficam entre um botão ser apertado e um reader agir sobre ele. Eles têm causas diferentes, e só um deles pode de fato ser removido. Um quarto, o bloqueio do reader, é aquele para o qual o buffering existe.

| Atraso | Causa | Removível? |
|---|---|---|
| Polling | O input é amostrado, não enviado | Não, só encurtado |
| O passo de ligação | O `Update` roda depois dos passos fixos do frame | Sim, com uma contrapartida |
| Sem rollback | Um passo que rodou sem a intenção está encerrado | Não |
| Bloqueio do reader | O reader ainda não está pronto para agir | Tolerado por uma janela |

## Atraso de polling {/* #polling-delay */}

Você fica sabendo de um aperto na próxima atualização de input, então em média espera metade de um intervalo de amostragem e, no pior caso, um intervalo inteiro. Amostrar com mais frequência encurta esse atraso. Nada o remove.

## O passo de ligação {/* #the-binding-step */}

Na configuração padrão, o Input System processa eventos logo antes de cada `Update`, e o `Update` roda *depois* dos passos fixos daquele frame. Um aperto capturado ali se liga ao próximo passo, que começa no próximo frame. A 60 fps isso dá cerca de 17 ms; a 12 fps, 83 ms.

Este é removível. O Input System pode, em vez disso, processar eventos logo antes de cada `FixedUpdate` (**Update Mode** definido como **Process Events In Fixed Update**), e então um aperto é atualizado e consumido no mesmo passo, sem nenhuma ligação adiante.

É uma contrapartida real, não um ganho de graça:

* Não há input por frame, que código de UI e de câmera normalmente quer.
* Dois toques que caem entre dois passos se juntam em um só, porque há apenas uma atualização de input por passo.

### Qual fase pode fazer polling de um dispositivo {/* #which-phase-may-poll-a-device */}

Isso decorre da configuração, e só dela. No modo padrão, ler o estado de um dispositivo no `FixedUpdate` loga um erro no editor e em builds de desenvolvimento, e silenciosamente retorna o valor dinâmico em um player de release. No modo fixo, o erro é o contrário.

Então faça o polling onde o input system do seu projeto atualiza, e marque o tick que aquela fase te dá. A [Regra 5](./input-surface.md#5-a-writer-in-fixedupdate-runs-before-its-readers-in-the-same-step-a-writer-in-update-binds-to-the-next-step) vale de qualquer forma, porque ela é escrita em termos de passos, não de fases.

| Update mode do Input System | Writer faz polling em | Marca |
|---|---|---|
| Process Events In Dynamic Update (padrão) | `Update` | `T + 1`, consumida no próximo passo |
| Process Events In Fixed Update | `FixedUpdate`, antes dos seus readers | `T`, consumida neste passo |

## Sem rollback {/* #no-rollback */}

Nada disso pode ser recuperado depois do fato. Quando um passo roda sem uma intenção, aquele passo está encerrado.

`Set` aceita um tick no passado, mas nenhum reader revisita um passo que já rodou, e a comparação com wrap simplesmente trata um tick retroativo como mais antigo. Retroagir faz uma intenção expirar mais cedo, nunca chegar mais cedo.

## Bloqueio do reader {/* #reader-gating */}

Um aperto costuma chegar enquanto o reader ainda não está disposto a agir, no meio de um ataque ou no ar. Uma [janela](./fixed-input-event.md#buffering) não reverte a espera, ela a tolera, e é por isso que ampliar uma janela parece remover lag, mesmo que o aperto tenha chegado tão tarde quanto antes.
