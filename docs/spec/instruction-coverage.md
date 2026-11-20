# Coverage LAD/FBD (instruction catalog TIA V21, S7-1200/1500)

Source of the list: `docs/catalog/*.txt|json` (table of contents of the installed Siemens help, `tools/instr_catalog.py`). 
Creation: `tools/gen_lad.py` (SD candidates, import + compile in TIA). Status: ok = created and compiled without errors; 
missing = not created so far (reason = last TIA message).
Summary: lad 164/164; fbd 165/165; extended_lad 141/141; extended_fbd 141/141; scl_scl 119/119; openuc_fbd 17/17; s7comm_fbd 6/6; modbus_lad 3/3; modbustcp_lad 4/4; openuc_lad 17/17; ptp_lad 8/8; s7comm_lad 6/6; uss_lad 4/4; webserver_lad 1/1; teleservice_lad 1/1; stl 157/157

## lad (LAD, sample 031_lad_all): 164 instructions, 164 ok

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | Bit logic operations | ---|   |---: Normally open contact | ok | FB_Lad_001 | |
| 002 | Bit logic operations | ---| / |---: Normally closed contact | ok | FB_Lad_002 | |
| 003 | Bit logic operations | --|NOT|--: Invert RLO | ok | FB_Lad_003 | |
| 004 | Bit logic operations | ---(   )---: Assignment | ok | FB_Lad_004 | |
| 005 | Bit logic operations | --( / )--: Negate assignment | ok | FB_Lad_005 | |
| 006 | Bit logic operations | ---( R )---: Reset output | ok | FB_Lad_006 | |
| 007 | Bit logic operations | ---( S )---: Set output | ok | FB_Lad_007 | |
| 008 | Bit logic operations | SET_BF: Set bit field | ok (SimaticML-Import) | FX_Lad_008 | |
| 009 | Bit logic operations | RESET_BF: Reset bit field | ok (SimaticML-Import) | FX_Lad_009 | |
| 010 | Bit logic operations | SR: Set/reset flip-flop | ok | FB_Lad_010 | |
| 011 | Bit logic operations | RS: Reset/set flip-flop | ok | FB_Lad_011 | |
| 012 | Bit logic operations | --|P|--: Scan operand for positive signal edge | ok | FB_Lad_012 | |
| 013 | Bit logic operations | --|N|--: Scan operand for negative signal edge | ok | FB_Lad_013 | |
| 014 | Bit logic operations | --(P)--: Set operand on positive signal edge | ok | FB_Lad_014 | |
| 015 | Bit logic operations | --(N)--: Set operand on negative signal edge | ok | FB_Lad_015 | |
| 016 | Bit logic operations | P_TRIG: Scan RLO for positive signal edge | ok | FB_Lad_016 | |
| 017 | Bit logic operations | N_TRIG: Scan RLO for negative signal edge | ok | FB_Lad_017 | |
| 018 | Bit logic operations | R_TRIG: Detect positive signal edge | ok | FB_Lad_018 | |
| 019 | Bit logic operations | F_TRIG: Detect negative signal edge | ok | FB_Lad_019 | |
| 020 | Timer operations | TP: Generate pulse | ok | FB_Lad_020 | |
| 021 | Timer operations | TON: Generate on-delay | ok | FB_Lad_021 | |
| 022 | Timer operations | TOF: Generate off-delay | ok | FB_Lad_022 | |
| 023 | Timer operations | TONR: Time accumulator | ok | FB_Lad_023 | |
| 024 | Timer operations | ---( TP )---: Start pulse timer | ok (SimaticML-Import) | FX_Lad_024 (039_fw41) | |
| 025 | Timer operations | ---( TON )---: Start on-delay timer | ok (SimaticML-Import) | FX_Lad_025 (039_fw41) | |
| 026 | Timer operations | ---( TOF )---: Start off-delay timer | ok (SimaticML-Import) | FX_Lad_026 (039_fw41) | |
| 027 | Timer operations | ---( TONR )---: Time accumulator | ok (SimaticML-Import) | FX_Lad_027 (039_fw41) | |
| 028 | Timer operations | ---( RT )---: Reset timer | ok (SimaticML-Import) | FX_Lad_028 | |
| 029 | Timer operations | ---( PT )---: Load time duration | ok (SimaticML-Import) | FX_Lad_029 | |
| 030 | Timer operations / Legacy | S_PULSE: Assign pulse timer parameters and start | ok | FB_Lad_030 (039_fw41) | |
| 031 | Timer operations / Legacy | S_PEXT: Assign extended pulse timer parameters and start | ok | FB_Lad_031 (039_fw41) | |
| 032 | Timer operations / Legacy | S_ODT: Assign on-delay timer parameters and start | ok | FB_Lad_032 (039_fw41) | |
| 033 | Timer operations / Legacy | S_ODTS: Assign retentive on-delay timer parameters and start | ok | FB_Lad_033 (039_fw41) | |
| 034 | Timer operations / Legacy | S_OFFDT: Assign off-delay timer parameters and start | ok | FB_Lad_034 (039_fw41) | |
| 035 | Timer operations / Legacy | ---( SP ): Start pulse timer | ok (SimaticML-Import) | FX_Lad_035 | |
| 036 | Timer operations / Legacy | ---( SE ): Start extended pulse timer | ok (SimaticML-Import) | FX_Lad_036 | |
| 037 | Timer operations / Legacy | ---( SD ): Start on-delay timer | ok (SimaticML-Import) | FX_Lad_037 | |
| 038 | Timer operations / Legacy | ---( SS ): Start retentive on-delay timer | ok (SimaticML-Import) | FX_Lad_038 | |
| 039 | Timer operations / Legacy | ---( SF ): Start off-delay timer | ok (SimaticML-Import) | FX_Lad_039 | |
| 040 | Counter operations | CTU: Count up | ok | FB_Lad_040 | |
| 041 | Counter operations | CTD: Count down | ok | FB_Lad_041 | |
| 042 | Counter operations | CTUD: Count up and down | ok | FB_Lad_042 | |
| 043 | Counter operations / Legacy | S_CU: Assign parameters and count up | ok | FB_Lad_043 (039_fw41) | |
| 044 | Counter operations / Legacy | S_CD: Assign parameters and count down | ok | FB_Lad_044 (039_fw41) | |
| 045 | Counter operations / Legacy | S_CUD: Assign parameters and count up / down | ok | FB_Lad_045 (039_fw41) | |
| 046 | Counter operations / Legacy | ---( SC ): Set counter value | ok (SimaticML-Import) | FX_Lad_046 (039_fw41) | |
| 047 | Counter operations / Legacy | ---( CU ): Count up | ok (SimaticML-Import) | FX_Lad_047 | |
| 048 | Counter operations / Legacy | ---( CD ): Count down | ok (SimaticML-Import) | FX_Lad_048 | |
| 049 | Comparator operations | CMP ==: Equal | ok | FB_Lad_049 | |
| 050 | Comparator operations | CMP <>: Not equal | ok | FB_Lad_050 | |
| 051 | Comparator operations | CMP >=: Greater or equal | ok | FB_Lad_051 | |
| 052 | Comparator operations | CMP <=: Less or equal | ok | FB_Lad_052 | |
| 053 | Comparator operations | CMP >: Greater than | ok | FB_Lad_053 | |
| 054 | Comparator operations | CMP <: Less than | ok | FB_Lad_054 | |
| 055 | Comparator operations | IN_RANGE: Value within range | ok | FB_Lad_055 | |
| 056 | Comparator operations | OUT_RANGE: Value outside range | ok | FB_Lad_056 | |
| 057 | Comparator operations | ----I OK I----: Check validity | ok (SimaticML-Import) | FX_Lad_057 | |
| 058 | Comparator operations | ----I NOT_OK I----: Check invalidity | ok | FB_Lad_058 (039_fw41) | |
| 059 | Comparator operations / VARIANT | EQ_Type: Compare data type for EQUAL with the data type of a tag | ok | FB_Lad_059 | |
| 060 | Comparator operations / VARIANT | NE_Type: Compare data type for UNEQUAL with the data type of a tag | ok | FB_Lad_060 | |
| 061 | Comparator operations / VARIANT | EQ_ElemType: Compare data type of an ARRAY element for EQUAL with the data type of a tag | ok | FB_Lad_061 | |
| 062 | Comparator operations / VARIANT | NE_ElemType: Compare data type of an ARRAY element for UNEQUAL with the data type of a tag | ok | FB_Lad_062 | |
| 063 | Comparator operations / VARIANT | IS_NULL: Check for EQUALS NULL pointer | ok | FB_Lad_063 | |
| 064 | Comparator operations / VARIANT | NOT_NULL: Check for UNEQUALS NULL pointer | ok | FB_Lad_064 | |
| 065 | Comparator operations / VARIANT | IS_ARRAY: Check for ARRAY | ok | FB_Lad_065 | |
| 066 | Comparator operations / VARIANT | EQ_TypeOfDB: Compare data type of an indirectly addressed DB for EQUAL with a data type | ok | FB_Lad_066 | |
| 067 | Math functions | CALCULATE: Calculate | ok (SimaticML-Import) | FB_LadCalc (034_flg_misc) | |
| 068 | Math functions | ADD: Add | ok | FB_Lad_068 | |
| 069 | Math functions | SUB: Subtract | ok | FB_Lad_069 | |
| 070 | Math functions | MUL: Multiply | ok | FB_Lad_070 | |
| 071 | Math functions | DIV: Divide | ok | FB_Lad_071 | |
| 072 | Math functions | MOD: Return remainder of division | ok | FB_Lad_072 | |
| 073 | Math functions | NEG: Create twos complement | ok | FB_Lad_073 | |
| 074 | Math functions | INC: Increment | ok (SimaticML-Import) | FX_Lad_074 | |
| 075 | Math functions | DEC: Decrement | ok (SimaticML-Import) | FX_Lad_075 | |
| 076 | Math functions | ABS: Form absolute value | ok | FB_Lad_076 | |
| 077 | Math functions | MIN: Get minimum | ok | FB_Lad_077 | |
| 078 | Math functions | MAX: Get maximum | ok | FB_Lad_078 | |
| 079 | Math functions | LIMIT: Set limit value | ok | FB_Lad_079 | |
| 080 | Math functions | SQR: Form square | ok | FB_Lad_080 | |
| 081 | Math functions | SQRT: Form square root | ok | FB_Lad_081 | |
| 082 | Math functions | LN: Form natural logarithm | ok | FB_Lad_082 | |
| 083 | Math functions | EXP: Form exponential value | ok | FB_Lad_083 | |
| 084 | Math functions | SIN: Form sine value | ok | FB_Lad_084 | |
| 085 | Math functions | COS: Form cosine value | ok | FB_Lad_085 | |
| 086 | Math functions | TAN: Form tangent value | ok | FB_Lad_086 | |
| 087 | Math functions | ASIN: Form arcsine value | ok | FB_Lad_087 | |
| 088 | Math functions | ACOS: Form arccosine value | ok | FB_Lad_088 | |
| 089 | Math functions | ATAN: Form arctangent value | ok | FB_Lad_089 | |
| 090 | Math functions | FRAC: Return fraction | ok | FB_Lad_090 | |
| 091 | Math functions | EXPT: Exponentiate | ok | FB_Lad_091 | |
| 092 | Move operations | MOVE: Move value | ok | FB_Lad_092 | |
| 093 | Move operations | Deserialize: Deserialize | ok (SimaticML-Import) | FX_Lad_093 (039_fw41) | |
| 094 | Move operations | Serialize: Serialize | ok (SimaticML-Import) | FX_Lad_094 (039_fw41) | |
| 095 | Move operations | MOVE_BLK: Move block | ok (SimaticML-Import) | FX_Lad_095 (039_fw41) | |
| 096 | Move operations | MOVE_BLK_VARIANT: Move block | ok (SimaticML-Import) | FX_Lad_096 | |
| 097 | Move operations | UMOVE_BLK: Move block uninterruptible | ok (SimaticML-Import) | FX_Lad_097 (039_fw41) | |
| 098 | Move operations | FILL_BLK: Fill block | ok (SimaticML-Import) | FX_Lad_098 (039_fw41) | |
| 099 | Move operations | UFILL_BLK: Fill block uninterruptible | ok (SimaticML-Import) | FX_Lad_099 (039_fw41) | |
| 100 | Move operations | SCATTER: Parse the bit sequence into individual bits | ok | FB_Lad_100 | |
| 101 | Move operations | SCATTER_BLK: Parse elements of an ARRAY of bit sequence into individual bits | ok (SimaticML-Import) | FX_Lad_101 | |
| 102 | Move operations | GATHER: Merge individual bits into a bit sequence | ok (SimaticML-Import) | FX_Lad_102 (039_fw41) | |
| 103 | Move operations | GATHER_BLK: Merge individual bits into multiple elements of an ARRAY of bit sequence | ok (SimaticML-Import) | FX_Lad_103 (039_fw41) | |
| 104 | Move operations | AssignmentAttempt: Attempt assignment to a reference | ok | FB_Lad_104 (039_fw41) | |
| 105 | Move operations | SWAP: Swap | ok | FB_Lad_105 | |
| 106 | Move operations / ARRAY DB | ReadFromArrayDB: Read from ARRAYdata block | ok (SimaticML-Import) | FX_Lad_106 | |
| 107 | Move operations / ARRAY DB | WriteToArrayDB: Write to ARRAY data block | ok (SimaticML-Import) | FX_Lad_107 | |
| 108 | Move operations / ARRAY DB | ReadFromArrayDBL: Read from ARRAY data block in load memory | ok (SimaticML-Import) | FX_Lad_108 (039_fw41) | |
| 109 | Move operations / ARRAY DB | WriteToArrayDBL: Write to ARRAY data block in load memory | ok (SimaticML-Import) | FX_Lad_109 (039_fw41) | |
| 110 | Move operations / VARIANT | VariantGet: Read out VARIANT tag value | ok | FB_Lad_110 | |
| 111 | Move operations / VARIANT | VariantPut: Write VARIANT tag value | ok | FB_Lad_111 | |
| 112 | Move operations / VARIANT | CountOfElements: Get number of ARRAY elements | ok | FB_Lad_112 | |
| 113 | Move operations / ARRAY[*] | LOWER_BOUND: Read out low ARRAY limit | ok | FB_Lad_113 | |
| 114 | Move operations / ARRAY[*] | UPPER_BOUND: Read out high ARRAY limit | ok | FB_Lad_114 | |
| 115 | Move operations / Legacy | FieldRead: Read field | ok (SimaticML-Import) | FX_Lad_115 | |
| 116 | Move operations / Legacy | FieldWrite: Write field | ok (SimaticML-Import) | FX_Lad_116 | |
| 117 | Move operations / Legacy | BLKMOV: Move block | ok | FB_Lad_117 | |
| 118 | Move operations / Legacy | UBLKMOV: Move block uninterruptible | ok | FB_Lad_118 | |
| 119 | Move operations / Legacy | FILL: Fill block | ok (SimaticML-Import) | FX_Lad_119 | |
| 120 | Conversion operations | CONVERT: Convert value | ok | FB_Lad_120 | |
| 121 | Conversion operations | ROUND: Round numerical value | ok | FB_Lad_121 | |
| 122 | Conversion operations | CEIL: Generate next higher integer from floating-point number | ok | FB_Lad_122 | |
| 123 | Conversion operations | FLOOR: Generate next lower integer from floating-point number | ok | FB_Lad_123 | |
| 124 | Conversion operations | TRUNC: Truncate numerical value | ok | FB_Lad_124 | |
| 125 | Conversion operations | SCALE_X: Scale | ok | FB_Lad_125 | |
| 126 | Conversion operations | NORM_X: Normalize | ok | FB_Lad_126 | |
| 127 | Conversion operations / Legacy | SCALE: Scale | ok | FB_Lad_127 | |
| 128 | Conversion operations / Legacy | UNSCALE: Unscale | ok | FB_Lad_128 | |
| 129 | Program control operations | ---( JMP ): Jump if RLO = 1 | ok (SimaticML-Import) | FB_LadJump (034_flg_misc) | |
| 130 | Program control operations | ---( JMPN ): Jump if RLO = 0 | ok (SimaticML-Import) | FX_Lad_130 (039_fw41) | |
| 131 | Program control operations | LABEL: Jump label | ok (SimaticML-Import) | FB_LadJump (034_flg_misc) | |
| 132 | Program control operations | JMP_LIST: Define jump list | ok (SimaticML-Import) | FX_Lad_132 (039_fw41) | |
| 133 | Program control operations | SWITCH: Jump distributor | ok (SimaticML-Import) | FX_Lad_133 (039_fw41) | |
| 134 | Program control operations | --(RET): Return | ok (SimaticML-Import) | FX_Lad_134 (039_fw41) | |
| 135 | Program control operations / Runtime control | ENDIS_PW: Locking and unlocking passwords of the CPU access levels | ok | FB_Lad_135 | |
| 136 | Program control operations / Runtime control | RE_TRIGR: Restart cycle monitoring time | ok | FB_Lad_136 | |
| 137 | Program control operations / Runtime control | STP: Exit program | ok | FB_Lad_137 | |
| 138 | Program control operations / Runtime control | GET_ERROR: Get error locally | ok | FB_Lad_138 | |
| 139 | Program control operations / Runtime control | GET_ERR_ID: Get error ID locally | ok (SimaticML-Import) | FX_Lad_139 | |
| 140 | Program control operations / Runtime control | INIT_RD: Initialize all retain data | ok | FB_Lad_140 | |
| 141 | Program control operations / Runtime control | WAIT: Configure time delay | ok | FB_Lad_141 | |
| 142 | Program control operations / Runtime control | RUNTIME: Measure program runtime | ok (SimaticML-Import) | FX_Lad_142 | |
| 143 | Word logic operations | AND: AND logic operation | ok | FB_Lad_143 | |
| 144 | Word logic operations | OR: OR logic operation | ok | FB_Lad_144 | |
| 145 | Word logic operations | XOR: EXCLUSIVE OR logic operation | ok | FB_Lad_145 | |
| 146 | Word logic operations | INVERT: Create ones complement | ok | FB_Lad_146 | |
| 147 | Word logic operations | DECO: Decode | ok (SimaticML-Import) | FX_Lad_147 | |
| 148 | Word logic operations | ENCO: Encode | ok (SimaticML-Import) | FX_Lad_148 | |
| 149 | Word logic operations | SEL: Select | ok | FB_Lad_149 | |
| 150 | Word logic operations | MUX: Multiplex | ok | FB_Lad_150 | |
| 151 | Word logic operations | DEMUX: Demultiplex | ok | FB_Lad_151 | |
| 152 | Shift and rotate | SHR: Shift right | ok | FB_Lad_152 | |
| 153 | Shift and rotate | SHL: Shift left | ok | FB_Lad_153 | |
| 154 | Shift and rotate | ROR: Rotate right | ok | FB_Lad_154 | |
| 155 | Shift and rotate | ROL: Rotate left | ok | FB_Lad_155 | |
| 156 | Legacy | DRUM: Implement sequencer | ok (SimaticML-Import) | FX_Lad_156 (039_fw41) | |
| 157 | Legacy | DCAT: Discrete control-timer alarm | ok (SimaticML-Import) | FX_Lad_157 (039_fw41) | |
| 158 | Legacy | MCAT: Motor control-timer alarm | ok (SimaticML-Import) | FX_Lad_158 (039_fw41) | |
| 159 | Legacy | IMC: Compare input bits with the bits of a mask | ok (SimaticML-Import) | FX_Lad_159 (039_fw41) | |
| 160 | Legacy | SMC: Compare scan matrix | ok (SimaticML-Import) | FX_Lad_160 (039_fw41) | |
| 161 | Legacy | LEAD_LAG: Lead and lag algorithm  | ok (SimaticML-Import) | FX_Lad_161 (039_fw41) | |
| 162 | Legacy | SEG: Create bit pattern for seven-segment display | ok | FB_Lad_162 | |
| 163 | Legacy | BCDCPL: Create tens complement | ok (SimaticML-Import) | FX_Lad_163 | |
| 164 | Legacy | BITSUM: Count number of set bits | ok | FB_Lad_164 | |

## fbd (FBD, sample 032_fbd_all): 165 instructions, 165 ok, 3 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | Bit logic operations | &: AND logic operation | ok | FB_Fbd_001 | |
| 002 | Bit logic operations | AND truth table | help page | | not an instruction of its own |
| 003 | Bit logic operations | >=1: OR logic operation | ok | FB_Fbd_003 | |
| 004 | Bit logic operations | OR truth table | help page | | not an instruction of its own |
| 005 | Bit logic operations | X: EXCLUSIVE OR logic operation | ok | FB_Fbd_005 | |
| 006 | Bit logic operations | EXCLUSIVE OR truth table | help page | | not an instruction of its own |
| 007 | Bit logic operations | Insert input | ok | FB_Fbd_007 | |
| 008 | Bit logic operations | Invert RLO | ok (SimaticML-Import) | FX_Fbd_008 | |
| 009 | Bit logic operations | =: Assignment | ok | FB_Fbd_009 | |
| 010 | Bit logic operations | /=: Negate assignment | ok | FB_Fbd_010 | |
| 011 | Bit logic operations | R: Reset output | ok | FB_Fbd_011 | |
| 012 | Bit logic operations | S: Set output | ok | FB_Fbd_012 | |
| 013 | Bit logic operations | SET_BF: Set bit field | ok (SimaticML-Import) | FX_Fbd_013 | |
| 014 | Bit logic operations | RESET_BF: Reset bit field | ok (SimaticML-Import) | FX_Fbd_014 | |
| 015 | Bit logic operations | SR: Set/reset flip-flop | ok | FB_Fbd_015 | |
| 016 | Bit logic operations | RS: Reset/set flip-flop | ok | FB_Fbd_016 | |
| 017 | Bit logic operations | P: Scan operand for positive signal edge | ok | FB_Fbd_017 | |
| 018 | Bit logic operations | N: Scan operand for negative signal edge | ok | FB_Fbd_018 | |
| 019 | Bit logic operations | P=: Set operand on positive signal edge | ok (SimaticML-Import) | FX_Fbd_019 | |
| 020 | Bit logic operations | N=: Set operand on negative signal edge | ok (SimaticML-Import) | FX_Fbd_020 | |
| 021 | Bit logic operations | P_TRIG: Scan RLO for positive signal edge | ok | FB_Fbd_021 | |
| 022 | Bit logic operations | N_TRIG: Scan RLO for negative signal edge | ok | FB_Fbd_022 | |
| 023 | Bit logic operations | R_TRIG: Detect positive signal edge | ok | FB_Fbd_023 | |
| 024 | Bit logic operations | F_TRIG: Detect negative signal edge | ok | FB_Fbd_024 | |
| 025 | Timer operations | TP: Generate pulse | ok | FB_Fbd_025 | |
| 026 | Timer operations | TON: Generate on-delay | ok | FB_Fbd_026 | |
| 027 | Timer operations | TOF: Generate off-delay | ok | FB_Fbd_027 | |
| 028 | Timer operations | TONR: Time accumulator | ok | FB_Fbd_028 | |
| 029 | Timer operations | TP: Start pulse timer | ok | FB_Fbd_029 | |
| 030 | Timer operations | TON: Start on-delay timer | ok | FB_Fbd_030 | |
| 031 | Timer operations | TOF: Start off-delay timer | ok | FB_Fbd_031 | |
| 032 | Timer operations | TONR: Time accumulator | ok | FB_Fbd_032 | |
| 033 | Timer operations | RT: Reset timer | ok (SimaticML-Import) | FX_Fbd_033 | |
| 034 | Timer operations | PT: Load time duration | ok (SimaticML-Import) | FX_Fbd_034 | |
| 035 | Timer operations / Legacy | S_PULSE: Assign pulse timer parameters and start | ok | FB_Fbd_035 (039_fw41) | |
| 036 | Timer operations / Legacy | S_PEXT: Assign extended pulse timer parameters and start | ok | FB_Fbd_036 (039_fw41) | |
| 037 | Timer operations / Legacy | S_ODT: Assign on-delay timer parameters and start | ok | FB_Fbd_037 (039_fw41) | |
| 038 | Timer operations / Legacy | S_ODTS: Assign retentive on-delay timer parameters and start | ok | FB_Fbd_038 (039_fw41) | |
| 039 | Timer operations / Legacy | S_OFFDT: Assign off-delay timer parameters and start | ok | FB_Fbd_039 (039_fw41) | |
| 040 | Timer operations / Legacy | SP: Start pulse timer | ok (SimaticML-Import) | FX_Fbd_040 | |
| 041 | Timer operations / Legacy | SE: Start extended pulse timer | ok (SimaticML-Import) | FX_Fbd_041 | |
| 042 | Timer operations / Legacy | SD: Start on-delay timer | ok (SimaticML-Import) | FX_Fbd_042 | |
| 043 | Timer operations / Legacy | SS: Start retentive on-delay timer | ok (SimaticML-Import) | FX_Fbd_043 | |
| 044 | Timer operations / Legacy | SF: Start off-delay timer | ok (SimaticML-Import) | FX_Fbd_044 | |
| 045 | Counter operations | CTU: Count up | ok | FB_Fbd_045 | |
| 046 | Counter operations | CTD: Count down | ok | FB_Fbd_046 | |
| 047 | Counter operations | CTUD: Count up and down | ok | FB_Fbd_047 | |
| 048 | Counter operations / Legacy | S_CU: Assign parameters and count up | ok | FB_Fbd_048 (039_fw41) | |
| 049 | Counter operations / Legacy | S_CD: Assign parameters and count down | ok | FB_Fbd_049 (039_fw41) | |
| 050 | Counter operations / Legacy | S_CUD: Assign parameters and count up / down | ok | FB_Fbd_050 (039_fw41) | |
| 051 | Counter operations / Legacy | SC: Set counter value | ok (SimaticML-Import) | FX_Fbd_051 (039_fw41) | |
| 052 | Counter operations / Legacy | CU: Count up | ok (SimaticML-Import) | FX_Fbd_052 | |
| 053 | Counter operations / Legacy | CD: Count down | ok (SimaticML-Import) | FX_Fbd_053 | |
| 054 | Comparator operations | CMP ==: Equal | ok | FB_Fbd_054 | |
| 055 | Comparator operations | CMP <>: Not equal | ok | FB_Fbd_055 | |
| 056 | Comparator operations | CMP >=: Greater or equal | ok | FB_Fbd_056 | |
| 057 | Comparator operations | CMP <=: Less or equal | ok | FB_Fbd_057 | |
| 058 | Comparator operations | CMP >: Greater than | ok | FB_Fbd_058 | |
| 059 | Comparator operations | CMP <: Less than | ok | FB_Fbd_059 | |
| 060 | Comparator operations | IN_RANGE: Value within range | ok | FB_Fbd_060 | |
| 061 | Comparator operations | OUT_RANGE: Value outside range | ok | FB_Fbd_061 | |
| 062 | Comparator operations | OK: Check validity | ok | FB_Fbd_062 (039_fw41) | |
| 063 | Comparator operations | NOT_OK: Check invalidity | ok | FB_Fbd_063 (039_fw41) | |
| 064 | Comparator operations / VARIANT | EQ_Type: Compare data type for EQUAL with the data type of a tag | ok | FB_Fbd_064 | |
| 065 | Comparator operations / VARIANT | NE_Type: Compare data type for UNEQUAL with the data type of a tag | ok | FB_Fbd_065 | |
| 066 | Comparator operations / VARIANT | EQ_ElemType: Compare data type of an ARRAY element for EQUAL with the data type of a tag | ok | FB_Fbd_066 | |
| 067 | Comparator operations / VARIANT | NE_ElemType: Compare data type of an ARRAY element for UNEQUAL with the data type of a tag | ok | FB_Fbd_067 | |
| 068 | Comparator operations / VARIANT | IS_NULL: Check for EQUALS NULL pointer | ok | FB_Fbd_068 | |
| 069 | Comparator operations / VARIANT | NOT_NULL: Check for UNEQUALS NULL pointer | ok | FB_Fbd_069 | |
| 070 | Comparator operations / VARIANT | IS_ARRAY: Check for ARRAY | ok | FB_Fbd_070 | |
| 071 | Comparator operations / VARIANT | EQ_TypeOfDB: Compare data type of an indirectly addressed DB for EQUAL with a data type | ok | FB_Fbd_071 | |
| 072 | Math functions | CALCULATE: Calculate | ok (SimaticML-Import) | FB_FbdCalc (034_flg_misc) | |
| 073 | Math functions | ADD: Add | ok | FB_Fbd_073 | |
| 074 | Math functions | SUB: Subtract | ok | FB_Fbd_074 | |
| 075 | Math functions | MUL: Multiply | ok | FB_Fbd_075 | |
| 076 | Math functions | DIV: Divide | ok | FB_Fbd_076 | |
| 077 | Math functions | MOD: Return remainder of division | ok | FB_Fbd_077 | |
| 078 | Math functions | NEG: Create twos complement | ok | FB_Fbd_078 | |
| 079 | Math functions | INC: Increment | ok (SimaticML-Import) | FX_Fbd_079 | |
| 080 | Math functions | DEC: Decrement | ok (SimaticML-Import) | FX_Fbd_080 | |
| 081 | Math functions | ABS: Form absolute value | ok | FB_Fbd_081 | |
| 082 | Math functions | MIN: Get minimum | ok (SimaticML-Import) | FX_Fbd_082 | |
| 083 | Math functions | MAX: Get maximum | ok (SimaticML-Import) | FX_Fbd_083 | |
| 084 | Math functions | LIMIT: Set limit value | ok | FB_Fbd_084 | |
| 085 | Math functions | SQR: Form square | ok | FB_Fbd_085 | |
| 086 | Math functions | SQRT: Form square root | ok | FB_Fbd_086 | |
| 087 | Math functions | LN: Form natural logarithm | ok | FB_Fbd_087 | |
| 088 | Math functions | EXP: Form exponential value | ok | FB_Fbd_088 | |
| 089 | Math functions | SIN: Form sine value | ok | FB_Fbd_089 | |
| 090 | Math functions | COS: Form cosine value | ok | FB_Fbd_090 | |
| 091 | Math functions | TAN: Form tangent value | ok | FB_Fbd_091 | |
| 092 | Math functions | ASIN: Form arcsine value | ok | FB_Fbd_092 | |
| 093 | Math functions | ACOS: Form arccosine value | ok | FB_Fbd_093 | |
| 094 | Math functions | ATAN: Form arctangent value | ok | FB_Fbd_094 | |
| 095 | Math functions | FRAC: Return fraction | ok | FB_Fbd_095 | |
| 096 | Math functions | EXPT: Exponentiate | ok | FB_Fbd_096 | |
| 097 | Move operations | MOVE: Move value | ok | FB_Fbd_097 | |
| 098 | Move operations | Deserialize: Deserialize | ok (SimaticML-Import) | FX_Fbd_098 (039_fw41) | |
| 099 | Move operations | Serialize: Serialize | ok (SimaticML-Import) | FX_Fbd_099 (039_fw41) | |
| 100 | Move operations | MOVE_BLK: Move block | ok (SimaticML-Import) | FX_Fbd_100 (039_fw41) | |
| 101 | Move operations | MOVE_BLK_VARIANT: Move block | ok (SimaticML-Import) | FX_Fbd_101 | |
| 102 | Move operations | UMOVE_BLK: Move block uninterruptible | ok (SimaticML-Import) | FX_Fbd_102 (039_fw41) | |
| 103 | Move operations | FILL_BLK: Fill block | ok (SimaticML-Import) | FX_Fbd_103 (039_fw41) | |
| 104 | Move operations | UFILL_BLK: Fill block uninterruptible | ok (SimaticML-Import) | FX_Fbd_104 (039_fw41) | |
| 105 | Move operations | SCATTER: Parse the bit sequence into individual bits | ok | FB_Fbd_105 | |
| 106 | Move operations | SCATTER_BLK: Parse elements of an ARRAY of bit sequence into individual bits | ok (SimaticML-Import) | FX_Fbd_106 | |
| 107 | Move operations | GATHER: Merge individual bits into a bit sequence | ok (SimaticML-Import) | FX_Fbd_107 (039_fw41) | |
| 108 | Move operations | GATHER_BLK: Merge individual bits into multiple elements of an ARRAY of bit sequence | ok (SimaticML-Import) | FX_Fbd_108 | |
| 109 | Move operations | SWAP: Swap | ok (SimaticML-Import) | FX_Fbd_109 (039_fw41) | |
| 110 | Move operations / ARRAY DB | ReadFromArrayDB: Read from ARRAY data block | ok (SimaticML-Import) | FX_Fbd_110 | |
| 111 | Move operations / ARRAY DB | WriteToArrayDB: Write to ARRAY data block | ok (SimaticML-Import) | FX_Fbd_111 | |
| 112 | Move operations / ARRAY DB | ReadFromArrayDBL: Read from ARRAY data block in load memory | ok (SimaticML-Import) | FX_Fbd_112 (039_fw41) | |
| 113 | Move operations / ARRAY DB | WriteToArrayDBL: Write to ARRAY data block in load memory | ok (SimaticML-Import) | FX_Fbd_113 (039_fw41) | |
| 114 | Move operations / VARIANT | VariantGet: Read out VARIANT tag value | ok | FB_Fbd_114 | |
| 115 | Move operations / VARIANT | VariantPut: Write VARIANT tag value | ok | FB_Fbd_115 | |
| 116 | Move operations / VARIANT | CountOfElements: Get number of ARRAY elements | ok | FB_Fbd_116 | |
| 117 | Move operations / ARRAY[*] | LOWER_BOUND: Read out low ARRAY limit | ok | FB_Fbd_117 | |
| 118 | Move operations / ARRAY[*] | UPPER_BOUND: Read out high ARRAY limit | ok | FB_Fbd_118 | |
| 119 | Move operations / Legacy | FieldRead: Read field | ok (SimaticML-Import) | FX_Fbd_119 | |
| 120 | Move operations / Legacy | FieldWrite: Write field | ok (SimaticML-Import) | FX_Fbd_120 | |
| 121 | Move operations / Legacy | BLKMOV: Move block | ok | FB_Fbd_121 | |
| 122 | Move operations / Legacy | UBLKMOV: Move block uninterruptible | ok | FB_Fbd_122 | |
| 123 | Move operations / Legacy | FILL: Fill block | ok (SimaticML-Import) | FX_Fbd_123 | |
| 124 | Conversion operations | CONVERT: Convert value | ok (SimaticML-Import) | FX_Fbd_124 | |
| 125 | Conversion operations | ROUND: Round numerical value | ok | FB_Fbd_125 | |
| 126 | Conversion operations | CEIL: Generate next higher integer from floating-point number | ok | FB_Fbd_126 | |
| 127 | Conversion operations | FLOOR: Generate next lower integer from floating-point number | ok | FB_Fbd_127 | |
| 128 | Conversion operations | TRUNC: Truncate numerical value | ok | FB_Fbd_128 | |
| 129 | Conversion operations | SCALE_X: Scale | ok | FB_Fbd_129 | |
| 130 | Conversion operations | NORM_X: Normalize | ok | FB_Fbd_130 | |
| 131 | Conversion operations / Legacy | SCALE: Scale | ok | FB_Fbd_131 | |
| 132 | Conversion operations / Legacy | UNSCALE: Unscale | ok | FB_Fbd_132 | |
| 133 | Program control operations | JMP: Jump if RLO = 1 | ok (SimaticML-Import) | FB_FbdJump (034_flg_misc) | |
| 134 | Program control operations | JMPN: Jump if RLO = 0 | ok (SimaticML-Import) | FX_Fbd_134 (039_fw41) | |
| 135 | Program control operations | LABEL: Jump label | ok (SimaticML-Import) | FB_FbdJump (034_flg_misc) | |
| 136 | Program control operations | JMP_LIST: Define jump list | ok (SimaticML-Import) | FX_Fbd_136 (039_fw41) | |
| 137 | Program control operations | SWITCH: Jump distributor | ok (SimaticML-Import) | FX_Fbd_137 (039_fw41) | |
| 138 | Program control operations | RET: Return | ok (SimaticML-Import) | FX_Fbd_138 (039_fw41) | |
| 139 | Program control operations / Runtime control | ENDIS_PW: Locking and unlocking passwords of the CPU access levels | ok | FB_Fbd_139 | |
| 140 | Program control operations / Runtime control | RE_TRIGR: Restart cycle monitoring time | ok | FB_Fbd_140 | |
| 141 | Program control operations / Runtime control | STP: Exit program | ok (SimaticML-Import) | FX_Fbd_141 | |
| 142 | Program control operations / Runtime control | GET_ERROR: Get error locally | ok | FB_Fbd_142 | |
| 143 | Program control operations / Runtime control | GET_ERR_ID: Get error ID locally | ok (SimaticML-Import) | FX_Fbd_143 | |
| 144 | Program control operations / Runtime control | INIT_RD: Initialize all retain data | ok | FB_Fbd_144 | |
| 145 | Program control operations / Runtime control | WAIT: Configure time delay | ok | FB_Fbd_145 | |
| 146 | Program control operations / Runtime control | RUNTIME: Measure program runtime | ok (SimaticML-Import) | FX_Fbd_146 | |
| 147 | Word logic operations | AND: AND logic operation | ok | FB_Fbd_147 | |
| 148 | Word logic operations | OR: OR logic operation | ok | FB_Fbd_148 | |
| 149 | Word logic operations | XOR: EXCLUSIVE OR logic operation | ok | FB_Fbd_149 | |
| 150 | Word logic operations | INVERT: Create ones complement | ok | FB_Fbd_150 | |
| 151 | Word logic operations | DECO: Decode | ok (SimaticML-Import) | FX_Fbd_151 | |
| 152 | Word logic operations | ENCO: Encode | ok (SimaticML-Import) | FX_Fbd_152 | |
| 153 | Word logic operations | SEL: Select | ok | FB_Fbd_153 | |
| 154 | Word logic operations | MUX: Multiplex | ok (SimaticML-Import) | FX_Fbd_154 | |
| 155 | Word logic operations | DEMUX: Demultiplex | ok | FB_Fbd_155 | |
| 156 | Shift and Rotate | SHR: Shift right | ok | FB_Fbd_156 | |
| 157 | Shift and Rotate | SHL: Shift left | ok | FB_Fbd_157 | |
| 158 | Shift and Rotate | ROR: Rotate right | ok (SimaticML-Import) | FX_Fbd_158 (039_fw41) | |
| 159 | Shift and Rotate | ROL: Rotate left | ok (SimaticML-Import) | FX_Fbd_159 (039_fw41) | |
| 160 | Legacy | DRUM: Implement sequencer | ok (SimaticML-Import) | FX_Fbd_160 (039_fw41) | |
| 161 | Legacy | DCAT: Discrete control-timer alarm | ok (SimaticML-Import) | FX_Fbd_161 (039_fw41) | |
| 162 | Legacy | MCAT: Motor control-timer alarm | ok (SimaticML-Import) | FX_Fbd_162 (039_fw41) | |
| 163 | Legacy | IMC: Compare input bits with the bits of a mask | ok (SimaticML-Import) | FX_Fbd_163 (039_fw41) | |
| 164 | Legacy | SMC: Compare scan matrix | ok (SimaticML-Import) | FX_Fbd_164 (039_fw41) | |
| 165 | Legacy | LEAD_LAG: Lead and lag algorithm  | ok (SimaticML-Import) | FX_Fbd_165 (039_fw41) | |
| 166 | Legacy | SEG: Create bit pattern for seven-segment display | ok | FB_Fbd_166 | |
| 167 | Legacy | BCDCPL: Create tens complement | ok (SimaticML-Import) | FX_Fbd_167 | |
| 168 | Legacy | BITSUM: Count number of set bits | ok | FB_Fbd_168 | |

## extended (LAD, sample 037_lad_xml): 141 instructions, 141 ok, 20 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | Date and time-of-day | T_COMP: Compare time tags | ok (SimaticML-Import) | FX_Lad_ex001 (039_fw41) | |
| 002 | Date and time-of-day | T_CONV: Convert times and extract | ok (SimaticML-Import) | FX_Lad_ex002 (039_fw41) | |
| 003 | Date and time-of-day | T_ADD: Add times | ok (SimaticML-Import) | FX_Lad_ex003 (039_fw41) | |
| 004 | Date and time-of-day | T_SUB: Subtract times | ok (SimaticML-Import) | FX_Lad_ex004 (039_fw41) | |
| 005 | Date and time-of-day | T_DIFF: Time difference | ok | FB_Lad_ex005 | |
| 006 | Date and time-of-day | T_COMBINE: Combine times | ok (SimaticML-Import) | FX_Lad_ex006 (039_fw41) | |
| 007 | Date and time-of-day / Time-of-day functions | WR_SYS_T: Set time-of-day | ok | FB_Lad_ex007 | |
| 008 | Date and time-of-day / Time-of-day functions | RD_SYS_T: Read time-of-day | ok | FB_Lad_ex008 | |
| 009 | Date and time-of-day / Time-of-day functions | RD_LOC_T: Read local time | ok | FB_Lad_ex009 | |
| 010 | Date and time-of-day / Time-of-day functions | WR_LOC_T: Write local time | ok (SimaticML-Import) | FX_Lad_ex010 (039_fw41) | |
| 011 | Date and time-of-day / Time-of-day functions | SET_TIMEZONE: Set time zone | ok | FB_Lad_ex011 | |
| 012 | Date and time-of-day / Time-of-day functions | SNC_RTCB: Synchronize slave clocks | ok | FB_Lad_ex012 | |
| 013 | Date and time-of-day / Time-of-day functions | TIME_TCK: Read time counter | ok | FB_Lad_ex013 | |
| 014 | Date and time-of-day / Time-of-day functions | RTM: Runtime meters | ok | FB_Lad_ex014 | |
| 015 | String + Char | S_MOVE: Move character string | ok | FB_Lad_ex015 | |
| 016 | String + Char | S_COMP: Compare character strings | ok (SimaticML-Import) | FX_Lad_ex016 (039_fw41) | |
| 017 | String + Char | S_CONV: Convert character string | ok (SimaticML-Import) | FX_Lad_ex017 (039_fw41) | |
| 018 | String + Char | STRG_VAL: Convert character string to numerical value | ok (SimaticML-Import) | FX_Lad_ex018 (039_fw41) | |
| 019 | String + Char | VAL_STRG: Convert numerical value to character string | ok (SimaticML-Import) | FX_Lad_ex019 (039_fw41) | |
| 020 | String + Char | Strg_TO_Chars: Convert character string to Array of CHAR | ok (SimaticML-Import) | FX_Lad_ex020 (039_fw41) | |
| 021 | String + Char | Chars_TO_Strg: Convert Array of CHAR to character string | ok (SimaticML-Import) | FX_Lad_ex021 (039_fw41) | |
| 022 | String + Char | MAX_LEN: Determine the length of a character string | ok (SimaticML-Import) | FX_Lad_ex022 (039_fw41) | |
| 023 | String + Char / JOIN: Joining multiple character strings | JOIN: Join multiple strings | ok (SimaticML-Import) | FX_Lad_ex023 (039_fw41) | |
| 024 | String + Char / SPLIT: Splitting a character array into multiple character strings | SPLIT: Splitting an array of characters into multiple strings | ok (SimaticML-Import) | FX_Lad_ex024 (039_fw41) | |
| 025 | String + Char | ATH: Convert ASCII string to hexadecimal number | ok | FB_Lad_ex025 | |
| 026 | String + Char | HTA: Convert hexadecimal number to ASCII string | ok | FB_Lad_ex026 | |
| 027 | String + Char | LEN: Determine the length of a character string | ok (SimaticML-Import) | FX_Lad_ex027 (039_fw41) | |
| 028 | String + Char | CONCAT: Combine character strings | ok (SimaticML-Import) | FX_Lad_ex028 (039_fw41) | |
| 029 | String + Char | LEFT: Read the left character of a character string | ok (SimaticML-Import) | FX_Lad_ex029 (039_fw41) | |
| 030 | String + Char | RIGHT: Read the right characters of a character string | ok (SimaticML-Import) | FX_Lad_ex030 (039_fw41) | |
| 031 | String + Char | MID: Read middle characters of a character string | ok (SimaticML-Import) | FX_Lad_ex031 (039_fw41) | |
| 032 | String + Char | DELETE: Delete characters in a character string | ok (SimaticML-Import) | FX_Lad_ex032 (039_fw41) | |
| 033 | String + Char | INSERT: Insert characters in a character string | ok (SimaticML-Import) | FX_Lad_ex033 (039_fw41) | |
| 034 | String + Char | REPLACE: Replace characters in a character string | ok (SimaticML-Import) | FX_Lad_ex034 (039_fw41) | |
| 035 | String + Char | FIND: Find characters in a character string | ok (SimaticML-Import) | FX_Lad_ex035 (039_fw41) | |
| 036 | String + Char / Runtime information | GetSymbolName: Read out a tag on the input parameter | ok | FB_Lad_ex036 | |
| 037 | String + Char / Runtime information | Querying GetSymbolPath: actual parameter at beginning of a call path | ok (SimaticML-Import) | FX_Lad_ex037 (039_fw41) | |
| 038 | String + Char / Runtime information | GetInstanceName: Read out name of the read instance | ok | FB_Lad_ex038 | |
| 039 | String + Char / Runtime information | GetInstancePath: Query composite global name of the block instance | ok | FB_Lad_ex039 | |
| 040 | String + Char / Runtime information | GetBlockName: Read out name of the block | ok | FB_Lad_ex040 | |
| 041 | Process image | UPDAT_PI: Update the process image inputs | ok | FB_Lad_ex041 | |
| 042 | Process image | UPDAT_PO: Update the process image outputs | ok | FB_Lad_ex042 | |
| 043 | Process image | SYNC_PI: Synchronize the process image inputs | ok | FB_Lad_ex043 | |
| 044 | Process image | SYNC_PO: Synchronize the process image outputs | ok | FB_Lad_ex044 | |
| 045 | Distributed I/O / RDREC: Read data record | RDREC: Read data record | ok | FB_Lad_ex045 | |
| 046 | Distributed I/O / WRREC: Write data record | WRREC: Write data record | ok | FB_Lad_ex046 | |
| 047 | Distributed I/O | GETIO: Read all inputs of a submodule | ok | FB_Lad_ex047 | |
| 048 | Distributed I/O | SETIO: Write all outputs of a submodule | ok | FB_Lad_ex048 | |
| 049 | Distributed I/O | GETIO_PART: Read inputs of a submodule | ok | FB_Lad_ex049 | |
| 050 | Distributed I/O | SETIO_PART: Write outputs of a submodule | ok | FB_Lad_ex050 | |
| 051 | Distributed I/O / RALRM: Receive interrupt | Description RALRM | ok (SimaticML-Import) | FX_Lad_ex051 (039_fw41) | |
| 052 | Distributed I/O / RALRM: Receive interrupt | Parameter STATUS | help page | | not an instruction of its own |
| 053 | Distributed I/O / RALRM: Receive interrupt | Parameter TINFO | help page | | not an instruction of its own |
| 054 | Distributed I/O / RALRM: Receive interrupt | Parameter AINFO | help page | | not an instruction of its own |
| 055 | Distributed I/O / RALRM: Receive interrupt | Destination area TINFO and AINFO | help page | | not an instruction of its own |
| 056 | Distributed I/O | D_ACT_DP: Activate/deactivate DP slaves | ok | FB_Lad_ex056 | |
| 057 | Distributed I/O / ReconfigIOSystem: Reconfigure IO system | ReconfigIOSystem: Reconfigure IO system | ok | FB_Lad_ex057 | |
| 058 | Distributed I/O / Other | RD_REC: Read data record from I/O | ok | FB_Lad_ex058 (039_fw41) | |
| 059 | Distributed I/O / Other | WR_REC: Write data record to I/O | ok | FB_Lad_ex059 (039_fw41) | |
| 060 | Distributed I/O / Other | DPRD_DAT: Read consistent data of a DP standard slave | ok | FB_Lad_ex060 | |
| 061 | Distributed I/O / Other | DPWR_DAT: Write consistent data of a DP standard slave | ok | FB_Lad_ex061 | |
| 062 | Distributed I/O / Other / iDevice / iSlave | RCVREC: Receive data record | ok | FB_Lad_ex062 | |
| 063 | Distributed I/O / Other / iDevice / iSlave | PRVREC: Make data record available | ok | FB_Lad_ex063 | |
| 064 | Distributed I/O / Other / PROFIBUS | DPSYC_FR: Synchronize DP slaves / Freeze inputs | ok | FB_Lad_ex064 | |
| 065 | Distributed I/O / Other / PROFIBUS | DPNRM_DG: Read diagnostics data from a DP slave | ok | FB_Lad_ex065 | |
| 066 | Distributed I/O / Other / PROFIBUS | DP_TOPOL: Determine topology for DP master system | ok | FB_Lad_ex066 | |
| 067 | PROFIenergy | Description of PROFIenergy | help page | | not an instruction of its own |
| 068 | PROFIenergy / IO controller | PE_START_END: Start and exit energy-saving mode | ok (SimaticML-Import) | FX_Lad_ex068 (039_fw41) | |
| 069 | PROFIenergy / IO controller | PE_CMD: Start and exit energy-saving mode / Read out status information | ok (SimaticML-Import) | FX_Lad_ex069 (039_fw41) | |
| 070 | PROFIenergy / IO controller | PE_DS3_Write_ET200S: Set power module switching behavior | ok (SimaticML-Import) | FX_Lad_ex070 (039_fw41) | |
| 071 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Description PE_WOL | ok | FB_Lad_ex071 (039_fw41) | |
| 072 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Parameter COM_RST | help page | | not an instruction of its own |
| 073 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Parameter START | help page | | not an instruction of its own |
| 074 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Parameter END | help page | | not an instruction of its own |
| 075 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | PENERGY parameter | help page | | not an instruction of its own |
| 076 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Parameter STATUS | help page | | not an instruction of its own |
| 077 | PROFIenergy / IO controller / PROFIenergy commands | Structure of the message frames | help page | | not an instruction of its own |
| 078 | PROFIenergy / IO controller / PROFIenergy commands | PI Command "Start_Pause" | help page | | not an instruction of its own |
| 079 | PROFIenergy / IO controller / PROFIenergy commands | PI Command "End_Pause" | help page | | not an instruction of its own |
| 080 | PROFIenergy / IO controller / PROFIenergy commands | PI command "Query_modes" - "List_Energy_Saving_Modes" | help page | | not an instruction of its own |
| 081 | PROFIenergy / IO controller / PROFIenergy commands | PI command "Query_modes" - "Get_Mode" | help page | | not an instruction of its own |
| 082 | PROFIenergy / IO controller / PROFIenergy commands | PI command "PEM_Status" | help page | | not an instruction of its own |
| 083 | PROFIenergy / IO controller / PROFIenergy commands | PI command "PE_Identify" | help page | | not an instruction of its own |
| 084 | PROFIenergy / IO controller / PROFIenergy commands | PI Command "Query_Measurement" - "Get_Measurement_list" | help page | | not an instruction of its own |
| 085 | PROFIenergy / IO controller / PROFIenergy commands | PI Command "Query_Measurement" - "Get_Measurement_values" | help page | | not an instruction of its own |
| 086 | PROFIenergy / iDevice / iSlave | PE_I_DEV: Control PROFIenergy commands in the I-Device | ok (SimaticML-Import) | FX_Lad_ex086 (039_fw41) | |
| 087 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Error_RSP: Generate negative answer to command | ok | FB_Lad_ex087 | |
| 088 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Start_RSP: Generate answer to command at start of pause | ok | FB_Lad_ex088 | |
| 089 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_End_RSP: Generate answer to command at end of pause | ok | FB_Lad_ex089 | |
| 090 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_List_Modes_RSP: Generate queried energy savings modes as answer | ok (SimaticML-Import) | FX_Lad_ex090 (039_fw41) | |
| 091 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Get_Mode_RSP: Generate queried energy data as answer | ok (SimaticML-Import) | FX_Lad_ex091 (039_fw41) | |
| 092 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_PEM_Status_RSP: Generate PEM status as answer | ok (SimaticML-Import) | FX_Lad_ex092 (039_fw41) | |
| 093 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Identify_RSP: Generate supported PROFIenergy commands as answer | ok (SimaticML-Import) | FX_Lad_ex093 (039_fw41) | |
| 094 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Measurement_List_RSP: Generate list of supported measured values as answer | ok | FB_Lad_ex094 | |
| 095 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Measurement_Value_RSP: Generate queried measured values as answer | ok | FB_Lad_ex095 | |
| 096 | Module parameter assignment | RD_DPAR: Read module data record | ok | FB_Lad_ex096 | |
| 097 | Module parameter assignment | RD_DPARA: Read module data record asynchronously | ok | FB_Lad_ex097 | |
| 098 | Module parameter assignment | RD_DPARM: Read data record from configured system data | ok | FB_Lad_ex098 | |
| 099 | Module parameter assignment | WR_DPARM: Transfer data record | ok | FB_Lad_ex099 | |
| 100 | Interrupts | ATTACH: Attach an OB to an interrupt event | ok | FB_Lad_ex100 | |
| 101 | Interrupts | DETACH: Detach an OB from an interrupt event | ok | FB_Lad_ex101 | |
| 102 | Interrupts / Cyclic interrupt | SET_CINT: Set cyclic interrupt parameters | ok | FB_Lad_ex102 | |
| 103 | Interrupts / Cyclic interrupt | QRY_CINT: Query cyclic interrupt parameters | ok | FB_Lad_ex103 | |
| 104 | Interrupts / Time-of-day interrupt | SET_TINT: Set time-of-day interrupt | ok | FB_Lad_ex104 | |
| 105 | Interrupts / Time-of-day interrupt | SET_TINTL: Set time-of-day interrupt | ok | FB_Lad_ex105 | |
| 106 | Interrupts / Time-of-day interrupt | CAN_TINT: Cancel time-of-day interrupt | ok | FB_Lad_ex106 | |
| 107 | Interrupts / Time-of-day interrupt | ACT_TINT: Enable time-of-day interrupt | ok | FB_Lad_ex107 | |
| 108 | Interrupts / Time-of-day interrupt | QRY_TINT: Query status of time-of-day interrupt | ok | FB_Lad_ex108 | |
| 109 | Interrupts / Time-delay interrupt | SRT_DINT: Start time-delay interrupt | ok | FB_Lad_ex109 | |
| 110 | Interrupts / Time-delay interrupt | CAN_DINT: Cancel time-delay interrupt | ok | FB_Lad_ex110 | |
| 111 | Interrupts / Time-delay interrupt | QRY_DINT: Query time-delay interrupt status | ok | FB_Lad_ex111 | |
| 112 | Interrupts / Synchronous errors | MSK_FLT: Mask synchronous error events | ok | FB_Lad_ex112 | |
| 113 | Interrupts / Synchronous errors | DMSK_FLT: Unmask synchronous error events | ok | FB_Lad_ex113 | |
| 114 | Interrupts / Synchronous errors | READ_ERR: Read out event status register | ok | FB_Lad_ex114 | |
| 115 | Interrupts / Asynchronous error event | DIS_IRT: Disable interrupt event | ok | FB_Lad_ex115 | |
| 116 | Interrupts / Asynchronous error event | EN_IRT: Enable interrupt event | ok | FB_Lad_ex116 | |
| 117 | Interrupts / Asynchronous error event | DIS_AIRT: Delay execution of higher priority interrupts and asynchronous error events | ok | FB_Lad_ex117 | |
| 118 | Interrupts / Asynchronous error event | EN_AIRT: Enable execution of higher priority interrupts and asynchronous error events | ok | FB_Lad_ex118 | |
| 119 | Alarms | Program_Alarm: Generate program alarm with associated values | ok (SimaticML-Import) | FX_Lad_ex119 (039_fw41) | |
| 120 | Alarms | Get_AlarmState: Output alarm status | ok | FB_Lad_ex120 | |
| 121 | Alarms | Gen_UsrMsg: Generate user diagnostic alarms | ok | FB_Lad_ex121 | |
| 122 | Alarms | Get_Alarm: Read pending alarm | ok | FB_Lad_ex122 (039_fw41) | |
| 123 | Alarms | Ack_Alarms: Acknowledge alarms | ok | FB_Lad_ex123 | |
| 124 | Diagnostics | RD_SINFO: Read current OB start information | ok | FB_Lad_ex124 | |
| 125 | Diagnostics | RT_INFO: Read out runtime statistics | ok | FB_Lad_ex125 | |
| 126 | Diagnostics | LED: Read LED status | ok | FB_Lad_ex126 | |
| 127 | Diagnostics | Get_IM_Data: Reading identification and maintenance data | ok | FB_Lad_ex127 | |
| 128 | Diagnostics | GET_NAME: Read out name of an IO device or a DP slave | ok | FB_Lad_ex128 | |
| 129 | Diagnostics | GetStationInfo: Read information of an IO device | ok | FB_Lad_ex129 | |
| 130 | Diagnostics | DeviceStates: Read module status information in an IO system | ok | FB_Lad_ex130 | |
| 131 | Diagnostics | ModuleStates: Read module status information of a module | ok | FB_Lad_ex131 | |
| 132 | Diagnostics | GEN_DIAG: Generate diagnostics information | ok | FB_Lad_ex132 | |
| 133 | Diagnostics | GET_DIAG: Read diagnostic information | ok | FB_Lad_ex133 | |
| 134 | Pulse | CTRL_PWM: Pulse-width modulation | ok | FB_Lad_ex134 (040_s71200) | |
| 135 | Recipes and data logging / Recipe functions | RecipeExport: Exporting recipes | ok | FB_Lad_ex135 | |
| 136 | Recipes and data logging / Recipe functions | RecipeImport: Importing recipes | ok | FB_Lad_ex136 | |
| 137 | Recipes and data logging / Data logging | Data logging - Overview | help page | | not an instruction of its own |
| 138 | Recipes and data logging / Data logging | DataLogCreate: Create data log | ok | FB_Lad_ex138 | |
| 139 | Recipes and data logging / Data logging / DataLogOpen: Open data log | DataLogOpen: Open data log | ok | FB_Lad_ex139 | |
| 140 | Recipes and data logging / Data logging / DataLogOpen: Open data log | DataLogOpen: Open data log | ok | FB_Lad_ex140 | |
| 141 | Recipes and data logging / Data logging | DataLogClear: Empty data log | ok | FB_Lad_ex141 | |
| 142 | Recipes and data logging / Data logging | DataLogWrite: Write data log | ok | FB_Lad_ex142 | |
| 143 | Recipes and data logging / Data logging | DataLogClose: Close data log | ok | FB_Lad_ex143 | |
| 144 | Recipes and data logging / Data logging | DataLogDelete: Delete data log | ok | FB_Lad_ex144 | |
| 145 | Recipes and data logging / Data logging / DataLogNewFile: Data log in new file | DataLogNewFile: Data log in new file | ok | FB_Lad_ex145 | |
| 146 | Recipes and data logging / Data logging / DataLogNewFile: Data log in new file | DataLogNewFile: Data log in new file | ok | FB_Lad_ex146 | |
| 147 | Data block functions | CREATE_DB: Create data block | ok | FB_Lad_ex147 | |
| 148 | Data block functions | READ_DBL: Read from data block in the load memory | ok | FB_Lad_ex148 | |
| 149 | Data block functions | WRIT_DBL: Write to data block in the load memory | ok | FB_Lad_ex149 | |
| 150 | Data block functions | ATTR_DB: Read data block attribute | ok | FB_Lad_ex150 | |
| 151 | Data block functions | DELETE_DB: Delete data block | ok | FB_Lad_ex151 | |
| 152 | Addressing | GEO2LOG: Determine hardware identifier from slot | ok | FB_Lad_ex152 | |
| 153 | Addressing | LOG2GEO: Determine slot from hardware identifier | ok | FB_Lad_ex153 | |
| 154 | Addressing | LOG2MOD: Determine the hardware identifier from addressing of STEP 7 V5.5 SPx | ok | FB_Lad_ex154 | |
| 155 | Addressing | IO2MOD: Determine hardware identifier from an IO address | ok | FB_Lad_ex155 | |
| 156 | Addressing | RD_ADDR: Determine IO addresses from the hardware identifier | ok | FB_Lad_ex156 | |
| 157 | Addressing / Legacy | GEO_LOG: Determine hardware identifier from slot | ok | FB_Lad_ex157 | |
| 158 | Addressing / Legacy | LOG_GEO: Determine slot from hardware identifier | ok | FB_Lad_ex158 | |
| 159 | Addressing / Legacy | RD_LGADR: Determine IO addresses from the hardware identifier | ok (SimaticML-Import) | FX_Lad_ex159 (039_fw41) | |
| 160 | Addressing / Legacy | GADR_LGC: Determine hardware identifier from slot and offset in the user data address area | ok | FB_Lad_ex160 | |
| 161 | Addressing / Legacy | LGC_GADR: Determine slot from hardware identifier | ok | FB_Lad_ex161 | |

## extended (FBD, sample 039_fw41): 141 instructions, 141 ok, 20 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | Date and time-of-day | T_COMP: Compare time tags | ok (SimaticML-Import) | FX_Fbd_ex001 (039_fw41) | |
| 002 | Date and time-of-day | T_CONV: Convert times and extract | ok (SimaticML-Import) | FX_Fbd_ex002 (039_fw41) | |
| 003 | Date and time-of-day | T_ADD: Add times | ok (SimaticML-Import) | FX_Fbd_ex003 (039_fw41) | |
| 004 | Date and time-of-day | T_SUB: Subtract times | ok (SimaticML-Import) | FX_Fbd_ex004 (039_fw41) | |
| 005 | Date and time-of-day | T_DIFF: Time difference | ok | FB_Fbd_ex005 (039_fw41) | |
| 006 | Date and time-of-day | T_COMBINE: Combine times | ok (SimaticML-Import) | FX_Fbd_ex006 (039_fw41) | |
| 007 | Date and time-of-day / Time-of-day functions | WR_SYS_T: Set time-of-day | ok | FB_Fbd_ex007 (039_fw41) | |
| 008 | Date and time-of-day / Time-of-day functions | RD_SYS_T: Read time-of-day | ok | FB_Fbd_ex008 (039_fw41) | |
| 009 | Date and time-of-day / Time-of-day functions | RD_LOC_T: Read local time | ok | FB_Fbd_ex009 (039_fw41) | |
| 010 | Date and time-of-day / Time-of-day functions | WR_LOC_T: Write local time | ok (SimaticML-Import) | FX_Fbd_ex010 (039_fw41) | |
| 011 | Date and time-of-day / Time-of-day functions | SET_TIMEZONE: Set time zone | ok | FB_Fbd_ex011 (039_fw41) | |
| 012 | Date and time-of-day / Time-of-day functions | SNC_RTCB: Synchronize slave clocks | ok | FB_Fbd_ex012 (039_fw41) | |
| 013 | Date and time-of-day / Time-of-day functions | TIME_TCK: Read time counter | ok | FB_Fbd_ex013 (039_fw41) | |
| 014 | Date and time-of-day / Time-of-day functions | RTM: Runtime meters | ok | FB_Fbd_ex014 (039_fw41) | |
| 015 | String + Char | S_MOVE: Move character string | ok | FB_Fbd_ex015 (039_fw41) | |
| 016 | String + Char | S_COMP: Compare character strings | ok (SimaticML-Import) | FX_Fbd_ex016 (039_fw41) | |
| 017 | String + Char | S_CONV: Convert character string | ok (SimaticML-Import) | FX_Fbd_ex017 (039_fw41) | |
| 018 | String + Char | STRG_VAL: Convert character string to numerical value | ok (SimaticML-Import) | FX_Fbd_ex018 (039_fw41) | |
| 019 | String + Char | VAL_STRG: Convert numerical value to character string | ok (SimaticML-Import) | FX_Fbd_ex019 (039_fw41) | |
| 020 | String + Char | Strg_TO_Chars: Convert character string to Array of CHAR | ok (SimaticML-Import) | FX_Fbd_ex020 (039_fw41) | |
| 021 | String + Char | Chars_TO_Strg: Convert Array of CHAR to character string | ok (SimaticML-Import) | FX_Fbd_ex021 (039_fw41) | |
| 022 | String + Char | MAX_LEN: Determine the length of a character string | ok (SimaticML-Import) | FX_Fbd_ex022 (039_fw41) | |
| 023 | String + Char / JOIN: Joining multiple character strings | JOIN: Join multiple strings | ok (SimaticML-Import) | FX_Fbd_ex023 (039_fw41) | |
| 024 | String + Char / SPLIT: Splitting a character array into multiple character strings | SPLIT: Splitting an array of characters into multiple strings | ok (SimaticML-Import) | FX_Fbd_ex024 (039_fw41) | |
| 025 | String + Char | ATH: Convert ASCII string to hexadecimal number | ok | FB_Fbd_ex025 (039_fw41) | |
| 026 | String + Char | HTA: Convert hexadecimal number to ASCII string | ok | FB_Fbd_ex026 (039_fw41) | |
| 027 | String + Char | LEN: Determine the length of a character string | ok (SimaticML-Import) | FX_Fbd_ex027 (039_fw41) | |
| 028 | String + Char | CONCAT: Combine character strings | ok (SimaticML-Import) | FX_Fbd_ex028 (039_fw41) | |
| 029 | String + Char | LEFT: Read the left character of a character string | ok (SimaticML-Import) | FX_Fbd_ex029 (039_fw41) | |
| 030 | String + Char | RIGHT: Read the right characters of a character string | ok (SimaticML-Import) | FX_Fbd_ex030 (039_fw41) | |
| 031 | String + Char | MID: Read middle characters of a character string | ok (SimaticML-Import) | FX_Fbd_ex031 (039_fw41) | |
| 032 | String + Char | DELETE: Delete characters in a character string | ok (SimaticML-Import) | FX_Fbd_ex032 (039_fw41) | |
| 033 | String + Char | INSERT: Insert characters in a character string | ok (SimaticML-Import) | FX_Fbd_ex033 (039_fw41) | |
| 034 | String + Char | REPLACE: Replace characters in a character string | ok (SimaticML-Import) | FX_Fbd_ex034 (039_fw41) | |
| 035 | String + Char | FIND: Find characters in a character string | ok (SimaticML-Import) | FX_Fbd_ex035 (039_fw41) | |
| 036 | String + Char / Runtime information | GetSymbolName: Read out a tag on the input parameter | ok | FB_Fbd_ex036 (039_fw41) | |
| 037 | String + Char / Runtime information | Querying GetSymbolPath: actual parameter at beginning of a call path | ok | FB_Fbd_ex037 (039_fw41) | |
| 038 | String + Char / Runtime information | GetInstanceName: Read out name of the read instance | ok | FB_Fbd_ex038 (039_fw41) | |
| 039 | String + Char / Runtime information | GetInstancePath: Query composite global name of the block instance | ok | FB_Fbd_ex039 (039_fw41) | |
| 040 | String + Char / Runtime information | GetBlockName: Read out name of the block | ok | FB_Fbd_ex040 (039_fw41) | |
| 041 | Process image | UPDAT_PI: Update the process image inputs | ok | FB_Fbd_ex041 (039_fw41) | |
| 042 | Process image | UPDAT_PO: Update the process image outputs | ok | FB_Fbd_ex042 (039_fw41) | |
| 043 | Process image | SYNC_PI: Synchronize the process image inputs | ok | FB_Fbd_ex043 (039_fw41) | |
| 044 | Process image | SYNC_PO: Synchronize the process image outputs | ok | FB_Fbd_ex044 (039_fw41) | |
| 045 | Distributed I/O / RDREC: Read data record | RDREC: Read data record | ok | FB_Fbd_ex045 (039_fw41) | |
| 046 | Distributed I/O / WRREC: Write data record | WRREC: Write data record | ok | FB_Fbd_ex046 (039_fw41) | |
| 047 | Distributed I/O | GETIO: Read all inputs of a submodule | ok | FB_Fbd_ex047 (039_fw41) | |
| 048 | Distributed I/O | SETIO: Write all outputs of a submodule | ok | FB_Fbd_ex048 (039_fw41) | |
| 049 | Distributed I/O | GETIO_PART: Read inputs of a submodule | ok | FB_Fbd_ex049 (039_fw41) | |
| 050 | Distributed I/O | SETIO_PART: Write outputs of a submodule | ok | FB_Fbd_ex050 (039_fw41) | |
| 051 | Distributed I/O / RALRM: Receive interrupt | Description RALRM | ok | FB_Fbd_ex051 (039_fw41) | |
| 052 | Distributed I/O / RALRM: Receive interrupt | Parameter STATUS | help page | | not an instruction of its own |
| 053 | Distributed I/O / RALRM: Receive interrupt | Parameter TINFO | help page | | not an instruction of its own |
| 054 | Distributed I/O / RALRM: Receive interrupt | Parameter AINFO | help page | | not an instruction of its own |
| 055 | Distributed I/O / RALRM: Receive interrupt | Destination area TINFO and AINFO | help page | | not an instruction of its own |
| 056 | Distributed I/O | D_ACT_DP: Activate/deactivate DP slaves | ok | FB_Fbd_ex056 (039_fw41) | |
| 057 | Distributed I/O / ReconfigIOSystem: Reconfigure IO system | ReconfigIOSystem: Reconfigure IO system | ok | FB_Fbd_ex057 (039_fw41) | |
| 058 | Distributed I/O / Other | RD_REC: Read data record from I/O | ok | FB_Fbd_ex058 (039_fw41) | |
| 059 | Distributed I/O / Other | WR_REC: Write data record to I/O | ok | FB_Fbd_ex059 (039_fw41) | |
| 060 | Distributed I/O / Other | DPRD_DAT: Read consistent data of a DP standard slave | ok | FB_Fbd_ex060 (039_fw41) | |
| 061 | Distributed I/O / Other | DPWR_DAT: Write consistent data of a DP standard slave | ok | FB_Fbd_ex061 (039_fw41) | |
| 062 | Distributed I/O / Other / iDevice / iSlave | RCVREC: Receive data record | ok | FB_Fbd_ex062 (039_fw41) | |
| 063 | Distributed I/O / Other / iDevice / iSlave | PRVREC: Make data record available | ok | FB_Fbd_ex063 (039_fw41) | |
| 064 | Distributed I/O / Other / PROFIBUS | DPSYC_FR: Synchronize DP slaves / Freeze inputs | ok | FB_Fbd_ex064 (039_fw41) | |
| 065 | Distributed I/O / Other / PROFIBUS | DPNRM_DG: Read diagnostics data from a DP slave | ok | FB_Fbd_ex065 (039_fw41) | |
| 066 | Distributed I/O / Other / PROFIBUS | DP_TOPOL: Determine topology for DP master system | ok | FB_Fbd_ex066 (039_fw41) | |
| 067 | PROFIenergy | Description of PROFIenergy | help page | | not an instruction of its own |
| 068 | PROFIenergy / IO controller | PE_START_END: Start and exit energy-saving mode | ok (SimaticML-Import) | FX_Fbd_ex068 (039_fw41) | |
| 069 | PROFIenergy / IO controller | PE_CMD: Start and exit energy-saving mode / Read out status information | ok (SimaticML-Import) | FX_Fbd_ex069 (039_fw41) | |
| 070 | PROFIenergy / IO controller | PE_DS3_Write_ET200S: Set power module switching behavior | ok (SimaticML-Import) | FX_Fbd_ex070 (039_fw41) | |
| 071 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Description PE_WOL | ok | FB_Fbd_ex071 (039_fw41) | |
| 072 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Parameter COM_RST | help page | | not an instruction of its own |
| 073 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Parameter START | help page | | not an instruction of its own |
| 074 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Parameter END | help page | | not an instruction of its own |
| 075 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | PENERGY parameter | help page | | not an instruction of its own |
| 076 | PROFIenergy / IO controller / PE_WOL: Starting and stopping energy-saving mode via WakeOnLan | Parameter STATUS | help page | | not an instruction of its own |
| 077 | PROFIenergy / IO controller / PROFIenergy commands | Structure of the message frames | help page | | not an instruction of its own |
| 078 | PROFIenergy / IO controller / PROFIenergy commands | PI Command "Start_Pause" | help page | | not an instruction of its own |
| 079 | PROFIenergy / IO controller / PROFIenergy commands | PI Command "End_Pause" | help page | | not an instruction of its own |
| 080 | PROFIenergy / IO controller / PROFIenergy commands | PI command "Query_modes" - "List_Energy_Saving_Modes" | help page | | not an instruction of its own |
| 081 | PROFIenergy / IO controller / PROFIenergy commands | PI command "Query_modes" - "Get_Mode" | help page | | not an instruction of its own |
| 082 | PROFIenergy / IO controller / PROFIenergy commands | PI command "PEM_Status" | help page | | not an instruction of its own |
| 083 | PROFIenergy / IO controller / PROFIenergy commands | PI command "PE_Identify" | help page | | not an instruction of its own |
| 084 | PROFIenergy / IO controller / PROFIenergy commands | PI Command "Query_Measurement" - "Get_Measurement_list" | help page | | not an instruction of its own |
| 085 | PROFIenergy / IO controller / PROFIenergy commands | PI Command "Query_Measurement" - "Get_Measurement_values" | help page | | not an instruction of its own |
| 086 | PROFIenergy / iDevice / iSlave | PE_I_DEV: Control PROFIenergy commands in the I-Device | ok (SimaticML-Import) | FX_Fbd_ex086 (039_fw41) | |
| 087 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Error_RSP: Generate negative answer to command | ok | FB_Fbd_ex087 (039_fw41) | |
| 088 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Start_RSP: Generate answer to command at start of pause | ok | FB_Fbd_ex088 (039_fw41) | |
| 089 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_End_RSP: Generate answer to command at end of pause | ok | FB_Fbd_ex089 (039_fw41) | |
| 090 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_List_Modes_RSP: Generate queried energy savings modes as answer | ok (SimaticML-Import) | FX_Fbd_ex090 (039_fw41) | |
| 091 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Get_Mode_RSP: Generate queried energy data as answer | ok (SimaticML-Import) | FX_Fbd_ex091 (039_fw41) | |
| 092 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_PEM_Status_RSP: Generate PEM status as answer | ok (SimaticML-Import) | FX_Fbd_ex092 (039_fw41) | |
| 093 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Identify_RSP: Generate supported PROFIenergy commands as answer | ok (SimaticML-Import) | FX_Fbd_ex093 (039_fw41) | |
| 094 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Measurement_List_RSP: Generate list of supported measured values as answer | ok | FB_Fbd_ex094 (039_fw41) | |
| 095 | PROFIenergy / iDevice / iSlave / Auxiliary blocks of the instruction PE_I_DEV | PE_Measurement_Value_RSP: Generate queried measured values as answer | ok | FB_Fbd_ex095 (039_fw41) | |
| 096 | Module parameter assignment | RD_DPAR: Read module data record | ok | FB_Fbd_ex096 (039_fw41) | |
| 097 | Module parameter assignment | RD_DPARA: Read module data record asynchronously | ok | FB_Fbd_ex097 (039_fw41) | |
| 098 | Module parameter assignment | RD_DPARM: Read data record from configured system data | ok | FB_Fbd_ex098 (039_fw41) | |
| 099 | Module parameter assignment | WR_DPARM: Transfer data record | ok | FB_Fbd_ex099 (039_fw41) | |
| 100 | Interrupts | ATTACH: Attach an OB to an interrupt event | ok | FB_Fbd_ex100 (039_fw41) | |
| 101 | Interrupts | DETACH: Detach an OB from an interrupt event | ok | FB_Fbd_ex101 (039_fw41) | |
| 102 | Interrupts / Cyclic interrupt | SET_CINT: Set cyclic interrupt parameters | ok | FB_Fbd_ex102 (039_fw41) | |
| 103 | Interrupts / Cyclic interrupt | QRY_CINT: Query cyclic interrupt parameters | ok | FB_Fbd_ex103 (039_fw41) | |
| 104 | Interrupts / Time-of-day interrupt | SET_TINT: Set time-of-day interrupt | ok | FB_Fbd_ex104 (039_fw41) | |
| 105 | Interrupts / Time-of-day interrupt | SET_TINTL: Set time-of-day interrupt | ok | FB_Fbd_ex105 (039_fw41) | |
| 106 | Interrupts / Time-of-day interrupt | CAN_TINT: Cancel time-of-day interrupt | ok | FB_Fbd_ex106 (039_fw41) | |
| 107 | Interrupts / Time-of-day interrupt | ACT_TINT: Enable time-of-day interrupt | ok | FB_Fbd_ex107 (039_fw41) | |
| 108 | Interrupts / Time-of-day interrupt | QRY_TINT: Query status of time-of-day interrupt | ok | FB_Fbd_ex108 (039_fw41) | |
| 109 | Interrupts / Time-delay interrupt | SRT_DINT: Start time-delay interrupt | ok | FB_Fbd_ex109 (039_fw41) | |
| 110 | Interrupts / Time-delay interrupt | CAN_DINT: Cancel time-delay interrupt | ok | FB_Fbd_ex110 (039_fw41) | |
| 111 | Interrupts / Time-delay interrupt | QRY_DINT: Query time-delay interrupt status | ok | FB_Fbd_ex111 (039_fw41) | |
| 112 | Interrupts / Synchronous errors | MSK_FLT: Mask synchronous error events | ok | FB_Fbd_ex112 (039_fw41) | |
| 113 | Interrupts / Synchronous errors | DMSK_FLT: Unmask synchronous error events | ok | FB_Fbd_ex113 (039_fw41) | |
| 114 | Interrupts / Synchronous errors | READ_ERR: Read out event status register | ok | FB_Fbd_ex114 (039_fw41) | |
| 115 | Interrupts / Asynchronous error event | DIS_IRT: Disable interrupt event | ok | FB_Fbd_ex115 (039_fw41) | |
| 116 | Interrupts / Asynchronous error event | EN_IRT: Enable interrupt event | ok | FB_Fbd_ex116 (039_fw41) | |
| 117 | Interrupts / Asynchronous error event | DIS_AIRT: Delay execution of higher priority interrupts and asynchronous error events | ok | FB_Fbd_ex117 (039_fw41) | |
| 118 | Interrupts / Asynchronous error event | EN_AIRT: Enable execution of higher priority interrupts and asynchronous error events | ok | FB_Fbd_ex118 (039_fw41) | |
| 119 | Alarms | Program_Alarm: Generate program alarm with associated values | ok (SimaticML-Import) | FX_Fbd_ex119 (039_fw41) | |
| 120 | Alarms | Get_AlarmState: Output alarm status | ok | FB_Fbd_ex120 (039_fw41) | |
| 121 | Alarms | Gen_UsrMsg: Generate user diagnostic alarms | ok | FB_Fbd_ex121 (039_fw41) | |
| 122 | Alarms | Get_Alarm: Read pending alarm | ok | FB_Fbd_ex122 (039_fw41) | |
| 123 | Alarms | Ack_Alarms: Acknowledge alarms | ok | FB_Fbd_ex123 (039_fw41) | |
| 124 | Diagnostics | RD_SINFO: Read current OB start information | ok | FB_Fbd_ex124 (039_fw41) | |
| 125 | Diagnostics | RT_INFO: Read out runtime statistics | ok | FB_Fbd_ex125 (039_fw41) | |
| 126 | Diagnostics | LED: Read LED status | ok | FB_Fbd_ex126 (039_fw41) | |
| 127 | Diagnostics | Get_IM_Data: Reading identification and maintenance data | ok | FB_Fbd_ex127 (039_fw41) | |
| 128 | Diagnostics | GET_NAME: Read out name of an IO device or a DP slave | ok | FB_Fbd_ex128 (039_fw41) | |
| 129 | Diagnostics | GetStationInfo: Read information of an IO device | ok | FB_Fbd_ex129 (039_fw41) | |
| 130 | Diagnostics | DeviceStates: Read module status information in an IO system | ok | FB_Fbd_ex130 (039_fw41) | |
| 131 | Diagnostics | ModuleStates: Read module status information of a module | ok | FB_Fbd_ex131 (039_fw41) | |
| 132 | Diagnostics | GEN_DIAG: Generate diagnostics information | ok | FB_Fbd_ex132 (039_fw41) | |
| 133 | Diagnostics | GET_DIAG: Read diagnostic information | ok | FB_Fbd_ex133 (039_fw41) | |
| 134 | Pulse | CTRL_PWM: Pulse-width modulation | ok | FB_Fbd_ex134 (040_s71200) | |
| 135 | Recipes and data logging / Recipe functions | RecipeExport: Exporting recipes | ok | FB_Fbd_ex135 (039_fw41) | |
| 136 | Recipes and data logging / Recipe functions | RecipeImport: Importing recipes | ok | FB_Fbd_ex136 (039_fw41) | |
| 137 | Recipes and data logging / Data logging | Data logging - Overview | help page | | not an instruction of its own |
| 138 | Recipes and data logging / Data logging | DataLogCreate: Create data log | ok | FB_Fbd_ex138 (039_fw41) | |
| 139 | Recipes and data logging / Data logging / DataLogOpen: Open data log | DataLogOpen: Open data log | ok | FB_Fbd_ex139 (039_fw41) | |
| 140 | Recipes and data logging / Data logging / DataLogOpen: Open data log | DataLogOpen: Open data log | ok | FB_Fbd_ex140 (039_fw41) | |
| 141 | Recipes and data logging / Data logging | DataLogClear: Empty data log | ok | FB_Fbd_ex141 (039_fw41) | |
| 142 | Recipes and data logging / Data logging | DataLogWrite: Write data log | ok | FB_Fbd_ex142 (039_fw41) | |
| 143 | Recipes and data logging / Data logging | DataLogClose: Close data log | ok | FB_Fbd_ex143 (039_fw41) | |
| 144 | Recipes and data logging / Data logging | DataLogDelete: Delete data log | ok | FB_Fbd_ex144 (039_fw41) | |
| 145 | Recipes and data logging / Data logging / DataLogNewFile: Data log in new file | DataLogNewFile: Data log in new file | ok | FB_Fbd_ex145 (039_fw41) | |
| 146 | Recipes and data logging / Data logging / DataLogNewFile: Data log in new file | DataLogNewFile: Data log in new file | ok | FB_Fbd_ex146 (039_fw41) | |
| 147 | Data block functions | CREATE_DB: Create data block | ok | FB_Fbd_ex147 (039_fw41) | |
| 148 | Data block functions | READ_DBL: Read from data block in the load memory | ok | FB_Fbd_ex148 (039_fw41) | |
| 149 | Data block functions | WRIT_DBL: Write to data block in the load memory | ok | FB_Fbd_ex149 (039_fw41) | |
| 150 | Data block functions | ATTR_DB: Read data block attribute | ok | FB_Fbd_ex150 (039_fw41) | |
| 151 | Data block functions | DELETE_DB: Delete data block | ok | FB_Fbd_ex151 (039_fw41) | |
| 152 | Addressing | GEO2LOG: Determine hardware identifier from slot | ok | FB_Fbd_ex152 (039_fw41) | |
| 153 | Addressing | LOG2GEO: Determine slot from hardware identifier | ok | FB_Fbd_ex153 (039_fw41) | |
| 154 | Addressing | LOG2MOD: Determine the hardware identifier from addressing of STEP 7 V5.5 SPx | ok | FB_Fbd_ex154 (039_fw41) | |
| 155 | Addressing | IO2MOD: Determine hardware identifier from an IO address | ok | FB_Fbd_ex155 (039_fw41) | |
| 156 | Addressing | RD_ADDR: Determine IO addresses from the hardware identifier | ok | FB_Fbd_ex156 (039_fw41) | |
| 157 | Addressing / Legacy | GEO_LOG: Determine hardware identifier from slot | ok | FB_Fbd_ex157 (039_fw41) | |
| 158 | Addressing / Legacy | LOG_GEO: Determine slot from hardware identifier | ok | FB_Fbd_ex158 (039_fw41) | |
| 159 | Addressing / Legacy | RD_LGADR: Determine IO addresses from the hardware identifier | ok (SimaticML-Import) | FX_Fbd_ex159 (039_fw41) | |
| 160 | Addressing / Legacy | GADR_LGC: Determine hardware identifier from slot and offset in the user data address area | ok | FB_Fbd_ex160 (039_fw41) | |
| 161 | Addressing / Legacy | LGC_GADR: Determine slot from hardware identifier | ok | FB_Fbd_ex161 (039_fw41) | |

## scl (SCL, sample 039_fw41): 119 instructions, 119 ok, 1 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | Bit logic operations | R_TRIG: Detect positive signal edge | ok | FB_Scl_sl001 (039_fw41) | |
| 002 | Bit logic operations | F_TRIG: Detect negative signal edge | ok | FB_Scl_sl002 (039_fw41) | |
| 003 | Timer operations | TP: Generate pulse | ok | FB_Scl_sl003 (039_fw41) | |
| 004 | Timer operations | TON: Generate on-delay | ok | FB_Scl_sl004 (039_fw41) | |
| 005 | Timer operations | TOF: Generate off-delay | ok | FB_Scl_sl005 (039_fw41) | |
| 006 | Timer operations | TONR: Time accumulator | ok | FB_Scl_sl006 (039_fw41) | |
| 007 | Timer operations | RESET_TIMER: Reset timer | ok | FB_Scl_m007 (039_fw41) | |
| 008 | Timer operations | PRESET_TIMER: Load time duration | ok | FB_Scl_m008 (039_fw41) | |
| 009 | Timer operations / Legacy | S_PULSE: Assign pulse timer parameters and start | ok | FB_Scl_sl009 (039_fw41) | |
| 010 | Timer operations / Legacy | S_PEXT: Assign extended pulse timer parameters and start | ok | FB_Scl_sl010 (039_fw41) | |
| 011 | Timer operations / Legacy | S_ODT: Assign on-delay timer parameters and start | ok | FB_Scl_sl011 (039_fw41) | |
| 012 | Timer operations / Legacy | S_ODTS: Assign retentive on-delay timer parameters and start | ok | FB_Scl_sl012 (039_fw41) | |
| 013 | Timer operations / Legacy | S_OFFDT: Assign off-delay timer parameters and start | ok | FB_Scl_sl013 (039_fw41) | |
| 014 | Counter operations | CTU: Count up | ok | FB_Scl_sl014 (039_fw41) | |
| 015 | Counter operations | CTD: Count down | ok | FB_Scl_sl015 (039_fw41) | |
| 016 | Counter operations | CTUD: Count up and down | ok | FB_Scl_sl016 (039_fw41) | |
| 017 | Counter operations / Legacy | S_CU: Assign parameters and count up | ok | FB_Scl_sl017 (039_fw41) | |
| 018 | Counter operations / Legacy | S_CD: Assign parameters and count down | ok | FB_Scl_sl018 (039_fw41) | |
| 019 | Counter operations / Legacy | S_CUD: Assign parameters and count up / down | ok | FB_Scl_sl019 (039_fw41) | |
| 020 | Comparator operations | TypeOf: Check data type of a VARIANT or ResolvedSymbol tag | ok | FB_Scl_m020 (039_fw41) | |
| 021 | Comparator operations | TypeOfElements: Check data type of an ARRAY element of a tag | ok | FB_Scl_m021 (039_fw41) | |
| 022 | Comparator operations | IS_ARRAY: Check for ARRAY | ok | FB_Scl_m022 (039_fw41) | |
| 023 | Comparator operations | TypeOfDB: Query data type of a DB | ok | FB_Scl_m023 (039_fw41) | |
| 024 | Math functions | ABS: Form absolute value | ok | FB_Scl_sl024 (039_fw41) | |
| 025 | Math functions | MIN: Get minimum | ok | FB_Scl_sl025 (039_fw41) | |
| 026 | Math functions | MAX: Get maximum | ok | FB_Scl_sl026 (039_fw41) | |
| 027 | Math functions | LIMIT: Set limit value | ok | FB_Scl_sl027 (039_fw41) | |
| 028 | Math functions | SQR: Form square | ok | FB_Scl_sl028 (039_fw41) | |
| 029 | Math functions | SQRT: Form square root | ok | FB_Scl_sl029 (039_fw41) | |
| 030 | Math functions | LN: Form natural logarithm | ok | FB_Scl_sl030 (039_fw41) | |
| 031 | Math functions | EXP: Form exponential value | ok | FB_Scl_sl031 (039_fw41) | |
| 032 | Math functions | SIN: Form sine value | ok | FB_Scl_sl032 (039_fw41) | |
| 033 | Math functions | COS: Form cosine value | ok | FB_Scl_sl033 (039_fw41) | |
| 034 | Math functions | TAN: Form tangent value | ok | FB_Scl_sl034 (039_fw41) | |
| 035 | Math functions | ASIN: Form arcsine value | ok | FB_Scl_sl035 (039_fw41) | |
| 036 | Math functions | ACOS: Form arccosine value | ok | FB_Scl_sl036 (039_fw41) | |
| 037 | Math functions | ATAN: Form arctangent value | ok | FB_Scl_sl037 (039_fw41) | |
| 038 | Math functions | FRAC: Return fraction | ok | FB_Scl_sl038 (039_fw41) | |
| 039 | Move operations | Deserialize: Deserialize | ok | FB_Scl_sl039 (039_fw41) | |
| 040 | Move operations | Serialize: Serialize | ok | FB_Scl_m040 (039_fw41) | |
| 041 | Move operations | MOVE_BLK: Move block | ok | FB_Scl_sl041 (039_fw41) | |
| 042 | Move operations | MOVE_BLK_VARIANT: Move block | ok | FB_Scl_m042 (039_fw41) | |
| 043 | Move operations | UMOVE_BLK: Move block uninterruptible | ok | FB_Scl_sl043 (039_fw41) | |
| 044 | Move operations | FILL_BLK: Fill block | ok | FB_Scl_m044 (039_fw41) | |
| 045 | Move operations | UFILL_BLK: Fill block uninterruptible | ok | FB_Scl_m045 (039_fw41) | |
| 046 | Move operations | SCATTER: Parse the bit sequence into individual bits | ok | FB_Scl_m046 (039_fw41) | |
| 047 | Move operations | SCATTER_BLK: Parse elements of an ARRAY of bit sequence into individual bits | ok | FB_Scl_sl047 (039_fw41) | |
| 048 | Move operations | GATHER: Merge individual bits into a bit sequence | ok | FB_Scl_m048 (039_fw41) | |
| 049 | Move operations | GATHER_BLK: Merge individual bits into multiple elements of an ARRAY of bit sequence | ok | FB_Scl_sl049 (039_fw41) | |
| 050 | Move operations | SWAP: Swap | ok | FB_Scl_sl050 (039_fw41) | |
| 051 | Move operations / ARRAY DB | ReadFromArrayDB: Read from array data block | ok | FB_Scl_m051 (039_fw41) | |
| 052 | Move operations / ARRAY DB | WriteToArrayDB: Write to array data block | ok | FB_Scl_m052 (039_fw41) | |
| 053 | Move operations / ARRAY DB | ReadFromArrayDBL: Read from array data block in load memory | ok | FB_Scl_sl053 (039_fw41) | |
| 054 | Move operations / ARRAY DB | WriteToArrayDBL: Write to array data block in load memory | ok | FB_Scl_sl054 (039_fw41) | |
| 055 | Move operations / Read/write access | PEEK: Read memory address | ok | FB_Scl_sl055 (039_fw41) | |
| 056 | Move operations / Read/write access | PEEK_BOOL: Read memory bit | ok | FC_SclRest (035_scl_rest) | |
| 057 | Move operations / Read/write access | POKE: Write memory address | ok | FC_SclRest (035_scl_rest) | |
| 058 | Move operations / Read/write access | POKE_BOOL: Write memory bit | ok | FB_Scl_sl058 (039_fw41) | |
| 059 | Move operations / Read/write access | POKE_BLK: Write memory area | ok | FB_Scl_sl059 (039_fw41) | |
| 060 | Move operations / Read/write access | READ_LITTLE: Read data in little endian format | ok | FB_Scl_m060 (039_fw41) | |
| 061 | Move operations / Read/write access | WRITE_LITTLE: Write data in little endian format | ok | FB_Scl_m061 (039_fw41) | |
| 062 | Move operations / Read/write access | READ_BIG: Read data in big endian format | ok | FB_Scl_m062 (039_fw41) | |
| 063 | Move operations / Read/write access | WRITE_BIG: Write data in big endian format | ok | FB_Scl_m063 (039_fw41) | |
| 064 | Move operations / VARIANT | VariantGet: Read out VARIANT tag value | ok | FB_Scl_sl064 (039_fw41) | |
| 065 | Move operations / VARIANT | VariantPut: Write VARIANT tag value | ok | FB_Scl_sl065 (039_fw41) | |
| 066 | Move operations / VARIANT | CountOfElements: Get number of ARRAY elements | ok | FB_Scl_m066 (039_fw41) | |
| 067 | Move operations / ARRAY[*] | LOWER_BOUND: Read out low ARRAY limit | ok | FB_Scl_m067 (039_fw41) | |
| 068 | Move operations / ARRAY[*] | UPPER_BOUND: Read out high ARRAY limit | ok | FB_Scl_m068 (039_fw41) | |
| 069 | Move operations / Legacy | BLKMOV: Move block | ok | FB_Scl_m069 (039_fw41) | |
| 070 | Move operations / Legacy | UBLKMOV: Move block uninterruptible | ok | FB_Scl_m070 (039_fw41) | |
| 071 | Move operations / Legacy | FILL: Fill block | ok | FB_Scl_m071 (039_fw41) | |
| 072 | Conversion operations | CONVERT: Convert value | ok | FB_Scl_m072 (039_fw41) | |
| 073 | Conversion operations | ROUND: Round numerical value | ok | FB_Scl_sl073 (039_fw41) | |
| 074 | Conversion operations | CEIL: Generate next higher integer from floating-point number | ok | FB_Scl_sl074 (039_fw41) | |
| 075 | Conversion operations | FLOOR: Generate next lower integer from floating-point number | ok | FB_Scl_sl075 (039_fw41) | |
| 076 | Conversion operations | TRUNC: Truncate numerical value | ok | FB_Scl_sl076 (039_fw41) | |
| 077 | Conversion operations | SCALE_X: Scale | ok | FB_Scl_sl077 (039_fw41) | |
| 078 | Conversion operations | NORM_X: Normalize | ok | FB_Scl_sl078 (039_fw41) | |
| 079 | Conversion operations | REF: Create a reference to a tag | ok | FC_SclRest (035_scl_rest) | |
| 080 | Conversion operations / VARIANT | VARIANT_TO_DB_ANY: Convert VARIANT to DB_ANY | ok | FB_Scl_m080 (039_fw41) | |
| 081 | Conversion operations / VARIANT | DB_ANY_TO_VARIANT: Convert DB_ANY to VARIANT | ok | FB_Scl_m081 (039_fw41) | |
| 082 | Conversion operations / Legacy | SCALE: Scale | ok | FB_Scl_m082 (039_fw41) | |
| 083 | Conversion operations / Legacy | UNSCALE: Unscale | ok | FB_Scl_m083 (039_fw41) | |
| 084 | Program control operations | IF: Run conditionally | ok | FC_SclCtl (030_scl_all) | |
| 085 | Program control operations | CASE: Create multiway branch | ok | FC_SclCtl (030_scl_all) | |
| 086 | Program control operations | FOR: Run in counting loop | ok | FC_SclCtl (030_scl_all) | |
| 087 | Program control operations | WHILE: Run if condition is met | ok | FC_SclCtl (030_scl_all) | |
| 088 | Program control operations | REPEAT: Run if condition is not met | ok | FC_SclCtl (030_scl_all) | |
| 089 | Program control operations | CONTINUE: Recheck loop condition | ok | FC_SclCtl (030_scl_all) | |
| 090 | Program control operations | EXIT: Exit loop immediately | ok | FC_SclCtl (030_scl_all) | |
| 091 | Program control operations | GOTO: Jump | ok | FC_SclCtl (030_scl_all) | |
| 092 | Program control operations | RETURN: Exit block | ok | FC_SclCtl (030_scl_all) | |
| 093 | Program control operations | (*...*): Insert a comment section | help page | | not an instruction of its own |
| 094 | Program control operations | REGION: Structure program code | ok | FC_SclCtl (030_scl_all) | |
| 095 | Program control operations / Runtime control | ENDIS_PW: Locking and unlocking passwords of the CPU access levels | ok | FB_Scl_m095 (039_fw41) | |
| 096 | Program control operations / Runtime control | RE_TRIGR: Restart cycle monitoring time | ok | FC_SclRt (039_fw41) | |
| 097 | Program control operations / Runtime control | STP: Exit program | ok | FC_SclRt (039_fw41) | |
| 098 | Program control operations / Runtime control | GET_ERROR: Get error locally | ok | FC_SclRt (039_fw41) | |
| 099 | Program control operations / Runtime control | GET_ERR_ID: Get error ID locally | ok | FC_SclRt (039_fw41) | |
| 100 | Program control operations / Runtime control | INIT_RD: Initialize all retain data | ok | FB_Scl_m100 (039_fw41) | |
| 101 | Program control operations / Runtime control | WAIT: Configure time delay | ok | FB_Scl_m101 (039_fw41) | |
| 102 | Program control operations / Runtime control | RUNTIME: Measure program runtime | ok | FB_Scl_m102 (039_fw41) | |
| 103 | Word logic operations | DECO: Decode | ok | FB_Scl_sl103 (039_fw41) | |
| 104 | Word logic operations | ENCO: Encode | ok | FB_Scl_sl104 (039_fw41) | |
| 105 | Word logic operations | SEL: Select | ok | FB_Scl_m105 (039_fw41) | |
| 106 | Word logic operations | MUX: Multiplex | ok | FB_Scl_m106 (039_fw41) | |
| 107 | Word logic operations | DEMUX: Demultiplex | ok | FB_Scl_m107 (039_fw41) | |
| 108 | Shift and Rotate | SHR: Shift right | ok | FB_Scl_sl108 (039_fw41) | |
| 109 | Shift and Rotate | SHL: Shift left | ok | FB_Scl_sl109 (039_fw41) | |
| 110 | Shift and Rotate | ROR: Rotate right | ok | FB_Scl_sl110 (039_fw41) | |
| 111 | Shift and Rotate | ROL: Rotate left | ok | FB_Scl_sl111 (039_fw41) | |
| 112 | Legacy | DRUM: Implement sequencer | ok | FB_Scl_sl112 (039_fw41) | |
| 113 | Legacy | DCAT: Discrete control-timer alarm | ok | FB_Scl_sl113 (039_fw41) | |
| 114 | Legacy | MCAT: Motor control-timer alarm | ok | FB_Scl_sl114 (039_fw41) | |
| 115 | Legacy | IMC: Compare input bits with the bits of a mask | ok | FB_Scl_sl115 (039_fw41) | |
| 116 | Legacy | SMC: Compare scan matrix | ok | FB_Scl_sl116 (039_fw41) | |
| 117 | Legacy | LEAD_LAG: Lead and lag algorithm | ok | FB_Scl_sl117 (039_fw41) | |
| 118 | Legacy | SEG: Create bit pattern for seven-segment display | ok | FB_Scl_sl118 (039_fw41) | |
| 119 | Legacy | BCDCPL: Create tens complement | ok | FB_Scl_sl119 (039_fw41) | |
| 120 | Legacy | BITSUM: Count number of set bits | ok | FB_Scl_sl120 (039_fw41) | |

## openuc (FBD, sample 039_fw41): 17 instructions, 17 ok, 7 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | TSEND_C: Establishing a connection and sending data | TSEND_C: Send data via Ethernet | ok | FB_Fbd_ou001 (039_fw41) | |
| 002 | TSEND_C: Establishing a connection and sending data | TSEND_C: Establishing a connection and sending data | ok | FB_Fbd_ou002 (039_fw41) | |
| 003 | TRCV_C: Establishing a connection and receiving data | TRCV_C: Receive data via Ethernet | ok | FB_Fbd_TrcvC (039_fw41) | |
| 004 | TRCV_C: Establishing a connection and receiving data | TRCV_C: Establishing a connection and receiving data | ok | FB_Fbd_ou004 (039_fw41) | |
| 005 | TMAIL_C: Transfer email | Description of TMAIL_C | ok (SimaticML-Import) | FX_Fbd_op005 (039_fw41) | |
| 006 | TMAIL_C: Transfer email | TO_S and CC parameters | help page | | not an instruction of its own |
| 007 | TMAIL_C: Transfer email | MAIL_ADDR_PARAM parameter | help page | | not an instruction of its own |
| 008 | TMAIL_C: Transfer email | DONE, BUSY and ERROR parameters | help page | | not an instruction of its own |
| 009 | TMAIL_C: Transfer email | STATUS parameter | help page | | not an instruction of its own |
| 010 | Other / TCON: Establishing a communication connection | TCON: Establish communication connection | ok | FB_Fbd_ou010 (039_fw41) | |
| 011 | Other / TCON: Establishing a communication connection | TCON: Establish communication connection | ok | FB_Fbd_ou011 (039_fw41) | |
| 012 | Other | TDISCON: Terminate communication connection | ok | FB_Fbd_ou012 (039_fw41) | |
| 013 | Other / TSEND: Send data via communication connection | TSEND: Send data via communication connection | ok | FB_Fbd_ou013 (039_fw41) | |
| 014 | Other / TSEND: Send data via communication connection | TSEND: Send data via communication connection | ok | FB_Fbd_ou014 (039_fw41) | |
| 015 | Other / TRCV: Receive data via communication connection | TRCV: Receive data via communication connection | ok | FB_Fbd_Trcv (039_fw41) | |
| 016 | Other / TRCV: Receive data via communication connection | TRCV: Receive data via communication connection | ok | FB_Fbd_ou016 (039_fw41) | |
| 017 | Other / Sending and receiving data via Ethernet (UDP) or FDL | TUSEND: Sending data | ok | FB_Fbd_ou017 (039_fw41) | |
| 018 | Other / Sending and receiving data via Ethernet (UDP) or FDL | TURCV: Receiving data | ok | FB_Fbd_ou018 (039_fw41) | |
| 019 | Other | T_RESET: Resetting the connection | ok | FB_Fbd_ou019 (039_fw41) | |
| 020 | Other | T_DIAG: Checking the connection | ok | FB_Fbd_ou020 (039_fw41) | |
| 021 | Other / T_CONFIG: Configure interface | Description T_CONFIG | ok (SimaticML-Import) | FX_Fbd_op021 (039_fw41) | |
| 022 | Other / T_CONFIG: Configure interface | Parameter CONF_DATA | help page | | not an instruction of its own |
| 023 | Other / T_CONFIG: Configure interface | DONE, BUSY and ERROR parameters | help page | | not an instruction of its own |
| 024 | Other / T_CONFIG: Configure interface | Parameter STATUS and ERR_LOC | help page | | not an instruction of its own |

## s7comm (FBD, sample 039_fw41): 6 instructions, 6 ok

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | S7 communication | GET: Read data from a remote CPU | ok | FB_Fbd_Get (039_fw41) | |
| 002 | S7 communication | PUT: Write data to a remote CPU | ok | FB_Fbd_Put (039_fw41) | |
| 003 | Other | USEND: Send data uncoordinated | ok | FB_Fbd_sc003 (039_fw41) | |
| 004 | Other | URCV: Receive data uncoordinated | ok | FB_Fbd_sc004 (039_fw41) | |
| 005 | Other | BSEND: Send data in segments | ok | FB_Fbd_sc005 (039_fw41) | |
| 006 | Other | BRCV: Receive data in segments | ok | FB_Fbd_sc006 (039_fw41) | |

## modbus (LAD, sample 038_comm_hw): 3 instructions, 3 ok, 7 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | MODBUS (RTU) | MB_COMM_LOAD: Configure port on the PtP module for Modbus RTU | ok | FB_Lad_mb001 | |
| 002 | MB_MASTER: Communicate via the PtP port as Modbus master | Description of MB_MASTER | ok | FB_Lad_mb002 | |
| 003 | MB_MASTER: Communicate via the PtP port as Modbus master | Parameter REQ | help page | | not an instruction of its own |
| 004 | MB_MASTER: Communicate via the PtP port as Modbus master | DATA_ADDR and MODE parameters | help page | | not an instruction of its own |
| 005 | MB_MASTER: Communicate via the PtP port as Modbus master | Parameter DATA_PTR | help page | | not an instruction of its own |
| 006 | MB_MASTER: Communicate via the PtP port as Modbus master | Instance DB of the "MB_MASTER" instruction | help page | | not an instruction of its own |
| 007 | MB_MASTER: Communicate via the PtP port as Modbus master | Sample program for a Modbus master | help page | | not an instruction of its own |
| 008 | MB_SLAVE: Communicate via the PtP port as Modbus slave | Description of MB_SLAVE | ok | FB_Lad_mb008 | |
| 009 | MB_SLAVE: Communicate via the PtP port as Modbus slave | Instance DB of the "MB_SLAVE" instruction | help page | | not an instruction of its own |
| 010 | MB_SLAVE: Communicate via the PtP port as Modbus slave | Sample program for a Modbus slave | help page | | not an instruction of its own |

## modbustcp (LAD, sample 038_comm_hw): 4 instructions, 4 ok, 14 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | MODBUS (TCP) for the library versions V2.1 and V3.x of the S7-1200 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | Description of MB_CLIENT | ok (SimaticML-Import) | FX_Lad_mo001 (039_fw41) | |
| 002 | MODBUS (TCP) for the library versions V2.1 and V3.x of the S7-1200 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | REQ and DISCONNECT parameters | help page | | not an instruction of its own |
| 003 | MODBUS (TCP) for the library versions V2.1 and V3.x of the S7-1200 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | MB_MODE, MB_DATA_ADDR and DATA_LEN parameters | help page | | not an instruction of its own |
| 004 | MODBUS (TCP) for the library versions V2.1 and V3.x of the S7-1200 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | MB_DATA_PTR parameter | help page | | not an instruction of its own |
| 005 | MODBUS (TCP) for the library versions V2.1 and V3.x of the S7-1200 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | Parameter STATUS  | help page | | not an instruction of its own |
| 006 | MODBUS (TCP) for the library versions V2.1 and V3.x of the S7-1200 CPUs / MB_SERVER: Communicating via PROFINET as a Modbus TCP server | Description of MB_SERVER | ok (SimaticML-Import) | FX_Lad_mo006 (039_fw41) | |
| 007 | MODBUS (TCP) for the library versions V2.1 and V3.x of the S7-1200 CPUs / MB_SERVER: Communicating via PROFINET as a Modbus TCP server | MB_HOLD_REG parameter | help page | | not an instruction of its own |
| 008 | MODBUS (TCP) for the library versions V2.1 and V3.x of the S7-1200 CPUs / MB_SERVER: Communicating via PROFINET as a Modbus TCP server | Parameter STATUS | help page | | not an instruction of its own |
| 009 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | Description of MB_CLIENT | ok (SimaticML-Import) | FX_Lad_mo009 (039_fw41) | |
| 010 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | REQ and DISCONNECT parameters | help page | | not an instruction of its own |
| 011 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | MB_MODE, MB_DATA_ADDR and MB_DATA_LEN parameters | help page | | not an instruction of its own |
| 012 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | MB_DATA_PTR parameter | help page | | not an instruction of its own |
| 013 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | CONNECT parameter | help page | | not an instruction of its own |
| 014 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_CLIENT: Communicating via PROFINET as a Modbus TCP client | Parameter STATUS  | help page | | not an instruction of its own |
| 015 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_SERVER: Communicating via PROFINET as a Modbus TCP server | Description of MB_SERVER | ok (SimaticML-Import) | FX_Lad_mo015 (039_fw41) | |
| 016 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_SERVER: Communicating via PROFINET as a Modbus TCP server | MB_HOLD_REG parameter | help page | | not an instruction of its own |
| 017 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_SERVER: Communicating via PROFINET as a Modbus TCP server | CONNECT parameter | help page | | not an instruction of its own |
| 018 | MODBUS (TCP) for the library versions V4.0 and later of the S7-1200 CPUs and as of V3.x of the S7-1500 CPUs / MB_SERVER: Communicating via PROFINET as a Modbus TCP server | Parameter STATUS | help page | | not an instruction of its own |

## openuc (LAD, sample 037_lad_xml): 17 instructions, 17 ok, 7 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | TSEND_C: Establishing a connection and sending data | TSEND_C: Send data via Ethernet | ok | FB_Lad_ou001 | |
| 002 | TSEND_C: Establishing a connection and sending data | TSEND_C: Establishing a connection and sending data | ok | FB_Lad_ou002 | |
| 003 | TRCV_C: Establishing a connection and receiving data | TRCV_C: Receive data via Ethernet | ok | FB_Lad_TrcvC (039_fw41) | |
| 004 | TRCV_C: Establishing a connection and receiving data | TRCV_C: Establishing a connection and receiving data | ok | FB_Lad_ou004 | |
| 005 | TMAIL_C: Transfer email | Description of TMAIL_C | ok (SimaticML-Import) | FX_Lad_op005 (039_fw41) | |
| 006 | TMAIL_C: Transfer email | TO_S and CC parameters | help page | | not an instruction of its own |
| 007 | TMAIL_C: Transfer email | MAIL_ADDR_PARAM parameter | help page | | not an instruction of its own |
| 008 | TMAIL_C: Transfer email | DONE, BUSY and ERROR parameters | help page | | not an instruction of its own |
| 009 | TMAIL_C: Transfer email | STATUS parameter | help page | | not an instruction of its own |
| 010 | Other / TCON: Establishing a communication connection | TCON: Establish communication connection | ok | FB_Lad_ou010 | |
| 011 | Other / TCON: Establishing a communication connection | TCON: Establish communication connection | ok | FB_Lad_ou011 | |
| 012 | Other | TDISCON: Terminate communication connection | ok | FB_Lad_ou012 | |
| 013 | Other / TSEND: Send data via communication connection | TSEND: Send data via communication connection | ok | FB_Lad_ou013 | |
| 014 | Other / TSEND: Send data via communication connection | TSEND: Send data via communication connection | ok | FB_Lad_ou014 | |
| 015 | Other / TRCV: Receive data via communication connection | TRCV: Receive data via communication connection | ok | FB_Lad_Trcv (039_fw41) | |
| 016 | Other / TRCV: Receive data via communication connection | TRCV: Receive data via communication connection | ok | FB_Lad_ou016 | |
| 017 | Other / Sending and receiving data via Ethernet (UDP) or FDL | TUSEND: Sending data | ok | FB_Lad_ou017 | |
| 018 | Other / Sending and receiving data via Ethernet (UDP) or FDL | TURCV: Receiving data | ok | FB_Lad_ou018 | |
| 019 | Other | T_RESET: Resetting the connection | ok | FB_Lad_ou019 | |
| 020 | Other | T_DIAG: Checking the connection | ok | FB_Lad_ou020 | |
| 021 | Other / T_CONFIG: Configure interface | Description T_CONFIG | ok (SimaticML-Import) | FX_Lad_op021 (039_fw41) | |
| 022 | Other / T_CONFIG: Configure interface | Parameter CONF_DATA | help page | | not an instruction of its own |
| 023 | Other / T_CONFIG: Configure interface | DONE, BUSY and ERROR parameters | help page | | not an instruction of its own |
| 024 | Other / T_CONFIG: Configure interface | Parameter STATUS and ERR_LOC | help page | | not an instruction of its own |

## ptp (LAD, sample 038_comm_hw): 8 instructions, 8 ok, 1 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | Point-to-point | PORT_CFG: Configure communication parameters dynamically | ok (SimaticML-Import) | FX_Lad_pt001 (039_fw41) | |
| 002 | Point-to-point | SEND_CFG: Configure serial transmission parameters dynamically | ok (SimaticML-Import) | FX_Lad_pt002 (039_fw41) | |
| 003 | Point-to-point | RCV_CFG: Configure serial receive parameters dynamically | ok (SimaticML-Import) | FX_Lad_pt003 (039_fw41) | |
| 004 | Point-to-point | SEND_PTP: Transmit send buffer data | ok (SimaticML-Import) | FX_Lad_pt004 (039_fw41) | |
| 005 | Point-to-point | RCV_PTP: Enable receive messages | ok (SimaticML-Import) | FX_Lad_pt005 (039_fw41) | |
| 006 | Point-to-point | RCV_RST: Delete receive buffer | ok | FB_Lad_pt006 | |
| 007 | Point-to-point | SGN_GET: Query RS-232 signals | ok | FB_Lad_pt007 | |
| 008 | Point-to-point | SGN_SET: Set RS-232 signals | ok | FB_Lad_pt008 | |
| 009 | Point-to-point | General status information of the communications blocks | help page | | not an instruction of its own |

## s7comm (LAD, sample 037_lad_xml): 6 instructions, 6 ok

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | S7 communication | GET: Read data from a remote CPU | ok | FB_Lad_Get (039_fw41) | |
| 002 | S7 communication | PUT: Write data to a remote CPU | ok | FB_Lad_Put (039_fw41) | |
| 003 | Other | USEND: Send data uncoordinated | ok | FB_Lad_sc003 | |
| 004 | Other | URCV: Receive data uncoordinated | ok | FB_Lad_sc004 | |
| 005 | Other | BSEND: Send data in segments | ok | FB_Lad_sc005 | |
| 006 | Other | BRCV: Receive data in segments | ok | FB_Lad_sc006 | |

## uss (LAD, sample 038_comm_hw): 4 instructions, 4 ok, 3 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | USS | Overview of USS instructions | help page | | not an instruction of its own |
| 002 | USS | Requirements for using the USS protocol | help page | | not an instruction of its own |
| 003 | USS | USS_PORT: Edit communication via USS network | ok | FB_Lad_us003 | |
| 004 | USS | USS_DRIVE: Swap data with drive | ok | FB_Lad_us004 | |
| 005 | USS | USS_RPM: Readout parameters from the drive | ok | FB_Lad_us005 | |
| 006 | USS | USS_WPM: Change parameters in the drive | ok | FB_Lad_us006 | |
| 007 | USS | Parameter STATUS of USS instructions | help page | | not an instruction of its own |

## webserver (LAD, sample 037_lad_xml): 1 instructions, 1 ok

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | WWW: Synchronizing user pages | WWW: Synchronizing user pages | ok | FB_Lad_ws001 | |

## teleservice (LAD, sample 038_comm_hw): 1 instructions, 1 ok, 2 help pages (parameter/example, not an instruction)

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | TM_MAIL: Transfer email | Description of TM_MAIL | ok (SimaticML-Import) | FX_Lad_te001 (039_fw41) | |
| 002 | TM_MAIL: Transfer email | Parameters TO_S, CC, and FROM | help page | | not an instruction of its own |
| 003 | TM_MAIL: Transfer email | STATUS and SFB_STATUS parameters | help page | | not an instruction of its own |


## Remaining list (generated from the result files)

Grouped by the kind of the last TIA message; the individual messages are in the tables above.


## stl (STL mnemonic, sample 064_stl_all): 157 instructions, 157 ok

Source: `docs/catalog/stl.txt` (ProgAWL15enUS), generator `tools/gen_stl.py`, import via `sourceImport` on CPU 1516, compile 0/0. One network per instruction with the title `<No> <mnemonic>`; checked: code of each network = `.awl` export (`ProjectTests.StlAgainstSource`). The 83 further pages of the STL catalog are calls of system instructions. All 80 with a parameter list are evidenced in sample 075_stl_calls (`tools/gen_stl_calls.py`, list `tools/sources/stl_calls/stl_calls.txt`, plus hand-written sources in `tools/sources/stl_calls2`–`stl_calls4`; code = `.awl` export, `ProjectTests.StlAgainstSource`). The last seven (2026-11-08, `stl_calls4`): the notation comes from the TIA editor (created via VNC, then exported as a source). PRESET_TIMER `{time_type := 'Time', timer_type := 'IEC_TIMER'}` with the parameter `"TIMER"`; CompType `{src_type := 'Variant', relation := 'EQ'}`; EQ/NE_TypeOfDB `{in2_type := 'TYPE_ID'}` with a type name as IN2 (Int is not allowed, IEC_TIMER works) and the output `Ret_Val`; ReadFromArrayDBL/WriteToArrayDBL and DRUM are FBs and are called as multi-instance (`CALL #inst`, `CALL DRUM, #inst` reports "does not belong to the called block"). TIA completes the templates from the operands when compiling (a source with a wrong template name is still compiled and exported with the correct one); for the array DB instances the export writes `{ptr_type := 'DB_ANY'}` and the parameters in lower case (`req`, `"db"`, `index`, `value`). In the editor itself PRESET_TIMER cannot be created without errors: the template line offers only timer types and PT remains "Please select a data type".

| No | Group | Instruction | Status | Block | Remark |
| --- | --- | --- | --- | --- | --- |
| 001 | Bit logic operations | A | ok | FB_StlBit | |
| 002 | Bit logic operations | AN | ok | FB_StlBit | |
| 003 | Bit logic operations | O | ok | FB_StlBit | |
| 004 | Bit logic operations | ON | ok | FB_StlBit | |
| 005 | Bit logic operations | X | ok | FB_StlBit | |
| 006 | Bit logic operations | XN | ok | FB_StlBit | |
| 007 | Bit logic operations | O (2) | ok | FB_StlBit | |
| 008 | Bit logic operations | A( | ok | FB_StlBit | |
| 009 | Bit logic operations | AN( | ok | FB_StlBit | |
| 010 | Bit logic operations | O( | ok | FB_StlBit | |
| 011 | Bit logic operations | ON( | ok | FB_StlBit | |
| 012 | Bit logic operations | X( | ok | FB_StlBit | |
| 013 | Bit logic operations | XN( | ok | FB_StlBit | |
| 014 | Bit logic operations | ) | ok | FB_StlBit | |
| 015 | Bit logic operations | = | ok | FB_StlBit | |
| 016 | Bit logic operations | R | ok | FB_StlBit | |
| 017 | Bit logic operations | S | ok | FB_StlBit | |
| 018 | Bit logic operations | NOT | ok | FB_StlBit | |
| 019 | Bit logic operations | SET | ok | FB_StlBit | |
| 020 | Bit logic operations | CLR | ok | FB_StlBit | |
| 021 | Bit logic operations | SAVE | ok | FB_StlBit | |
| 022 | Bit logic operations | FN | ok | FB_StlBit | |
| 023 | Bit logic operations | FP | ok | FB_StlBit | |
| 024 | Timer operations | FR | ok | FB_StlTimer | |
| 025 | Timer operations | L | ok | FB_StlTimer | |
| 026 | Timer operations | LC | ok | FB_StlTimer | |
| 027 | Timer operations | R | ok | FB_StlTimer | |
| 028 | Timer operations | SP | ok | FB_StlTimer | |
| 029 | Timer operations | SE | ok | FB_StlTimer | |
| 030 | Timer operations | SD | ok | FB_StlTimer | |
| 031 | Timer operations | SS | ok | FB_StlTimer | |
| 032 | Timer operations | SF | ok | FB_StlTimer | |
| 033 | Counter operations | FR | ok | FB_StlTimer | |
| 034 | Counter operations | L | ok | FB_StlTimer | |
| 035 | Counter operations | LC | ok | FB_StlTimer | |
| 036 | Counter operations | R | ok | FB_StlTimer | |
| 037 | Counter operations | S | ok | FB_StlTimer | |
| 038 | Counter operations | CU | ok | FB_StlTimer | |
| 039 | Counter operations | CD | ok | FB_StlTimer | |
| 040 | Comparator operations | ? I | ok | FB_StlMath | |
| 041 | Comparator operations | ? D | ok | FB_StlMath | |
| 042 | Comparator operations | ? R | ok | FB_StlMath | |
| 043 | Integers | +I | ok | FB_StlMath | |
| 044 | Integers | -I | ok | FB_StlMath | |
| 045 | Integers | *I | ok | FB_StlMath | |
| 046 | Integers | /I | ok | FB_StlMath | |
| 047 | Integers | +D | ok | FB_StlMath | |
| 048 | Integers | -D | ok | FB_StlMath | |
| 049 | Integers | *D | ok | FB_StlMath | |
| 050 | Integers | /D | ok | FB_StlMath | |
| 051 | Integers | + | ok | FB_StlMath | |
| 052 | Integers | INC | ok | FB_StlMath | |
| 053 | Integers | DEC | ok | FB_StlMath | |
| 054 | Integers | MOD | ok | FB_StlMath | |
| 055 | Floating-point numbers | +R | ok | FB_StlMath | |
| 056 | Floating-point numbers | -R | ok | FB_StlMath | |
| 057 | Floating-point numbers | *R | ok | FB_StlMath | |
| 058 | Floating-point numbers | /R | ok | FB_StlMath | |
| 059 | Floating-point numbers | ABS | ok | FB_StlMath | |
| 060 | Advanced floating-point numbers | SQR | ok | FB_StlMath | |
| 061 | Advanced floating-point numbers | SQRT | ok | FB_StlMath | |
| 062 | Advanced floating-point numbers | EXP | ok | FB_StlMath | |
| 063 | Advanced floating-point numbers | LN | ok | FB_StlMath | |
| 064 | Advanced floating-point numbers | SIN | ok | FB_StlMath | |
| 065 | Advanced floating-point numbers | ASIN | ok | FB_StlMath | |
| 066 | Advanced floating-point numbers | COS | ok | FB_StlMath | |
| 067 | Advanced floating-point numbers | ACOS | ok | FB_StlMath | |
| 068 | Advanced floating-point numbers | TAN | ok | FB_StlMath | |
| 069 | Advanced floating-point numbers | ATAN | ok | FB_StlMath | |
| 070 | Load | L | ok | FB_StlLoad | |
| 071 | Load | L STW | ok | FB_StlLoad | |
| 072 | Load | LAR1 | ok | FB_StlLoad | |
| 073 | Load | LAR1 <D> | ok | FB_StlLoad | |
| 074 | Load | LAR1 AR2 | ok | FB_StlLoad | |
| 075 | Load | LAR2 | ok | FB_StlLoad | |
| 076 | Load | LAR2 <D> | ok | FB_StlLoad | |
| 077 | Transfer | T | ok | FB_StlLoad | |
| 078 | Transfer | T STW | ok | FB_StlLoad | |
| 079 | Transfer | CAR | ok | FB_StlLoad | |
| 080 | Transfer | TAR1 | ok | FB_StlLoad | |
| 081 | Transfer | TAR1 <D> | ok | FB_StlLoad | |
| 082 | Transfer | TAR1 AR2 | ok | FB_StlLoad | |
| 083 | Transfer | TAR2 | ok | FB_StlLoad | |
| 084 | Transfer | TAR2 <D> | ok | FB_StlLoad | |
| 085 | Conversion operations | BTI | ok | FB_StlMath | |
| 086 | Conversion operations | ITB | ok | FB_StlMath | |
| 087 | Conversion operations | BTD | ok | FB_StlMath | |
| 088 | Conversion operations | ITD | ok | FB_StlMath | |
| 089 | Conversion operations | DTB | ok | FB_StlMath | |
| 090 | Conversion operations | DTR | ok | FB_StlMath | |
| 091 | Conversion operations | INVI | ok | FB_StlMath | |
| 092 | Conversion operations | INVD | ok | FB_StlMath | |
| 093 | Conversion operations | NEGI | ok | FB_StlMath | |
| 094 | Conversion operations | NEGD | ok | FB_StlMath | |
| 095 | Conversion operations | NEGR | ok | FB_StlMath | |
| 096 | Conversion operations | CAW | ok | FB_StlMath | |
| 097 | Conversion operations | CAD | ok | FB_StlMath | |
| 098 | Conversion operations | RND | ok | FB_StlMath | |
| 099 | Conversion operations | TRUNC | ok | FB_StlMath | |
| 100 | Conversion operations | RND+ | ok | FB_StlMath | |
| 101 | Conversion operations | RND- | ok | FB_StlMath | |
| 102 | Jumps | Jump label | ok | FB_StlJump | |
| 103 | Jumps | JU | ok | FB_StlJump | |
| 104 | Jumps | JC | ok | FB_StlJump | |
| 105 | Jumps | JCN | ok | FB_StlJump | |
| 106 | Jumps | JCB | ok | FB_StlJump | |
| 107 | Jumps | JNB | ok | FB_StlJump | |
| 108 | Jumps | JBI | ok | FB_StlJump | |
| 109 | Jumps | JNBI | ok | FB_StlJump | |
| 110 | Jumps | JO | ok | FB_StlJump | |
| 111 | Jumps | JOS | ok | FB_StlJump | |
| 112 | Jumps | JZ | ok | FB_StlJump | |
| 113 | Jumps | JN | ok | FB_StlJump | |
| 114 | Jumps | JP | ok | FB_StlJump | |
| 115 | Jumps | JM | ok | FB_StlJump | |
| 116 | Jumps | JPZ | ok | FB_StlJump | |
| 117 | Jumps | JMZ | ok | FB_StlJump | |
| 118 | Jumps | JUO | ok | FB_StlJump | |
| 119 | Jumps | JL | ok | FB_StlJump | |
| 120 | Jumps | LOOP | ok | FB_StlJump | |
| 121 | Data blocks | OPN | ok | FB_StlDb | |
| 122 | Data blocks | OPNDI | ok | FB_StlDb | |
| 123 | Data blocks | CDB | ok | FB_StlDb | |
| 124 | Data blocks | L DBLG | ok | FB_StlDb | |
| 125 | Data blocks | L DBNO | ok | FB_StlDb | |
| 126 | Data blocks | L DILG | ok | FB_StlDb | |
| 127 | Data blocks | L DINO | ok | FB_StlDb | |
| 128 | Code blocks | BE | ok | FB_StlBe | |
| 129 | Code blocks | BEC | ok | FB_StlCall | |
| 130 | Code blocks | BEU | ok | FB_StlCall | |
| 131 | Code blocks | CALL | ok | FB_StlCall | |
| 132 | Code blocks | CC | ok | FB_StlCall | |
| 133 | Code blocks | UC | ok | FB_StlCall | |
| 134 | Word logic operations | AW | ok | FB_StlMath | |
| 135 | Word logic operations | OW | ok | FB_StlMath | |
| 136 | Word logic operations | XOW | ok | FB_StlMath | |
| 137 | Word logic operations | AD | ok | FB_StlMath | |
| 138 | Word logic operations | OD | ok | FB_StlMath | |
| 139 | Word logic operations | XOD | ok | FB_StlMath | |
| 140 | Shift | SSI | ok | FB_StlMath | |
| 141 | Shift | SSD | ok | FB_StlMath | |
| 142 | Shift | SLW | ok | FB_StlMath | |
| 143 | Shift | SRW | ok | FB_StlMath | |
| 144 | Shift | SLD | ok | FB_StlMath | |
| 145 | Shift | SRD | ok | FB_StlMath | |
| 146 | Rotate | RLD | ok | FB_StlMath | |
| 147 | Rotate | RRD | ok | FB_StlMath | |
| 148 | Rotate | RLDA | ok | FB_StlMath | |
| 149 | Rotate | RRDA | ok | FB_StlMath | |
| 150 | Accumulator | TAK | ok | FB_StlMath | |
| 151 | Accumulator | PUSH | ok | FB_StlMath | |
| 152 | Accumulator | POP | ok | FB_StlMath | |
| 153 | Address register | +AR1 | ok | FB_StlLoad | |
| 154 | Address register | +AR2 | ok | FB_StlLoad | |
| 155 | Null instructions | BLD | ok | FB_StlMath | |
| 156 | Null instructions | NOP 0 | ok | FB_StlMath | |
| 157 | Null instructions | NOP 1 | ok | FB_StlMath | |
