# Design system: Backoffice Avarias

Criado a partir das imagens de referência em `C:\Users\PC\Downloads\backoffice_design_referencia` (cores medidas pixel a pixel). Versão completa: https://claude.ai/artifact/UUEeyNicAkfyWHj4Epw1ag (privado).

Os tokens estão copiados no topo de `src/Avarias.Api/wwwroot/css/styles.css`: **uma mudança de cor faz-se nos dois sítios**.

## Cores

| Papel | Tokens | Cor |
|---|---|---|
| Neutros (quase todo o ecrã) | `--surface-200`, `--surface-0`, `--ink-900`, `--ink-700`, `--line-300` | `#EFEFF1`, `#FFFFFF`, `#16171B`, `#494B4F`, `#D7D7D9` |
| Marca e "onde estou" | `--purple-700`, `--purple-100`, `--purple-200` | `#582C83`, `#F2EBFA`, `#DCC8EF` |
| Ação principal | `--action-600` | `#1B7FD5` |
| Destruir | `--danger-600` | `#D43432` (em texto) |
| Estado | `--success-*`, `--warning-*`, `--error-*`, `--info-*` | Resolvido verde, Em resolução amarelo, Em análise azul-claro, Pendente cinzento |

## Regras

- Letra Fira Sans na interface e Montserrat no nome da aplicação.
- Urgência com ponto colorido; prioridade em pílula roxa.
- Painel em tabela; em telemóvel cada linha empilha.
- Ações irreversíveis abrem um modal de confirmação.
- Sem o logótipo da referência (é de outra empresa); o logótipo da escola aparece só no login.

## Contraste

O texto passa AA, com uma exceção vinda da referência: branco sobre `--action-600` dá 4,2:1. Para cumprir AA à letra, usar `#1769B0` no botão principal.
