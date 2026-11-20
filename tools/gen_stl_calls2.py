"""STL (AWL) calls of the system instructions that gen_stl_calls.py cannot take from a LAD/FBD block (C3 rest, sample 075):
hand-written calls on a fixed interface. Writes tools/sources/stl_calls2/FB_StlC_<nnn>.awl.
Usage: python -I tools/gen_stl_calls2.py"""
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(ROOT, "tools", "sources", "stl_calls2")

ITF = """   VAR_INPUT
      a : Bool;
      i1 : Int;
      i2 : Int;
      di1 : DInt;
      r1 : Real;
      w1 : Word;
      dw1 : DWord;
      b1 : Byte;
      u1 : UInt;
      ud1 : UDInt;
   END_VAR
   VAR_OUTPUT
      q : Bool;
      io : Int;
      dio : DInt;
      ro : Real;
      wo : Word;
      dwo : DWord;
      bo : Byte;
      udo : UDInt;
   END_VAR
   VAR
      src : Array[0..9] of Int;
      dst : Array[0..9] of Int;
      bits : Array[0..31] of Bool;
      bytes : Array[0..9] of Byte;
      err : ErrorStruct;
      rt : LReal;
   END_VAR
"""

# interface of the second batch: adds VARIANT and ARRAY[*] in/out parameters
ITF2 = ITF.replace("   VAR\n", """   VAR_IN_OUT
      v : Variant;
      arrs : Array[*] of Int;
      pos : DInt;
   END_VAR
   VAR\n""").replace("      rt : LReal;\n", "      rt : LReal;\n      tim : IEC_TIMER;\n      words : Array[0..3] of Word;\n")

# second batch (catalog no -> (name, call lines)), on ITF2
CALLS2 = {
    7: ("RESET_TIMER", ["CALL RESET_TIMER", "(  TIMER := #tim", ");"]),
    17: ("IS_NULL", ["CALL IS_NULL", "(  OPERAND := #v ,", "   RET_VAL := #q", ");"]),
    41: ("PEEK", ["CALL PEEK", "{value_type := 'Byte'}", "(  AREA := #b1 ,", "   DBNUMBER := #di1 ,", "   BYTEOFFSET := #di1 ,", "   RET_VAL := #bo", ");"]),
    42: ("PEEK_BOOL", ["CALL PEEK_BOOL", "(  AREA := #b1 ,", "   DBNUMBER := #di1 ,", "   BYTEOFFSET := #di1 ,", "   BITOFFSET := #i1 ,", "   RET_VAL := #q", ");"]),
    43: ("POKE", ["CALL POKE", "{value_type := 'Byte'}", "(  AREA := #b1 ,", "   DBNUMBER := #di1 ,", "   BYTEOFFSET := #di1 ,", "   VALUE := #b1", ");"]),
    44: ("POKE_BOOL", ["CALL POKE_BOOL", "(  AREA := #b1 ,", "   DBNUMBER := #di1 ,", "   BYTEOFFSET := #di1 ,", "   BITOFFSET := #i1 ,", "   VALUE := #a", ");"]),
    45: ("POKE_BLK", ["CALL POKE_BLK", "(  AREA_SRC := #b1 ,", "   DBNUMBER_SRC := #di1 ,", "   BYTEOFFSET_SRC := #di1 ,", "   AREA_DEST := #b1 ,",
                      "   DBNUMBER_DEST := #di1 ,", "   BYTEOFFSET_DEST := #di1 ,", "   COUNT := #di1", ");"]),
    46: ("READ_LITTLE", ["CALL READ_LITTLE", "{value_type := 'Int'}", "(  SRC_ARRAY := #bytes ,", "   DEST_VARIABLE := #io ,", "   POS := #pos ,", "   RET_VAL := #io", ");"]),
    47: ("WRITE_LITTLE", ["CALL WRITE_LITTLE", "{value_type := 'Int'}", "(  SRC_VARIABLE := #i1 ,", "   DEST_ARRAY := #bytes ,", "   POS := #pos ,", "   RET_VAL := #io", ");"]),
    48: ("READ_BIG", ["CALL READ_BIG", "{value_type := 'Int'}", "(  SRC_ARRAY := #bytes ,", "   DEST_VARIABLE := #io ,", "   POS := #pos ,", "   RET_VAL := #io", ");"]),
    49: ("WRITE_BIG", ["CALL WRITE_BIG", "{value_type := 'Int'}", "(  SRC_VARIABLE := #i1 ,", "   DEST_ARRAY := #bytes ,", "   POS := #pos ,", "   RET_VAL := #io", ");"]),
    50: ("VariantGet", ["CALL VariantGet", "(  SRC := #v ,", "   DST := #io", ");"]),
    51: ("VariantPut", ["CALL VariantPut", "(  SRC := #i1 ,", "   DST := #v", ");"]),
    52: ("CountOfElements", ["CALL CountOfElements", "(  IN := #v ,", "   RET_VAL := #udo", ");"]),
    53: ("LOWER_BOUND", ["CALL LOWER_BOUND", "(  ARR := #arrs ,", "   DIM := #ud1 ,", "   RET_VAL := #dio", ");"]),
    54: ("UPPER_BOUND", ["CALL UPPER_BOUND", "(  ARR := #arrs ,", "   DIM := #ud1 ,", "   RET_VAL := #dio", ");"]),
    57: ("FILL", ["CALL FILL", "(  BVAL := #v ,", "   RET_VAL := #io ,", "   BLK := #v", ");"]),
    13: ("EQ_Type", ["CALL EQ_Type", "{value_type := 'Int'}", "(  IN1 := #v ,", "   IN2 := #i1 ,", "   RET_VAL := #q", ");"]),
    14: ("NE_Type", ["CALL NE_Type", "{value_type := 'Int'}", "(  IN1 := #v ,", "   IN2 := #i1 ,", "   RET_VAL := #q", ");"]),
    15: ("EQ_ElemType", ["CALL EQ_ElemType", "{value_type := 'Int'}", "(  IN1 := #v ,", "   IN2 := #i1 ,", "   RET_VAL := #q", ");"]),
    16: ("NE_ElemType", ["CALL NE_ElemType", "{value_type := 'Int'}", "(  IN1 := #v ,", "   IN2 := #i1 ,", "   RET_VAL := #q", ");"]),
    18: ("NOT_NULL", ["CALL NOT_NULL", "(  OPERAND := #v ,", "   RET_VAL := #q", ");"]),
    19: ("IS_ARRAY", ["CALL IS_ARRAY", "(  OPERAND := #v ,", "   RET_VAL := #q", ");"]),
    36: ("GATHER_BLK", ["CALL GATHER_BLK", "{value_type := 'Word'}", "(  IN := #bits[0] ,", "   COUNT_OUT := #u1 ,", "   OUT := #words[0]", ");"]),
}

# catalog no -> (name, call lines)
CALLS = {
    25: ("MOVE", ["CALL MOVE", "{value_type := 'Int'}", "(  IN := #i1 ,", "   OUT := #io", ");"]),
    28: ("MOVE_BLK", ["CALL MOVE_BLK", "(  IN := #src[0] ,", "   COUNT := #u1 ,", "   OUT := #dst[0]", ");"]),
    30: ("UMOVE_BLK", ["CALL UMOVE_BLK", "(  IN := #src[0] ,", "   COUNT := #u1 ,", "   OUT := #dst[0]", ");"]),
    31: ("FILL_BLK", ["CALL FILL_BLK", "(  IN := #i1 ,", "   COUNT := #u1 ,", "   OUT := #dst[0]", ");"]),
    32: ("UFILL_BLK", ["CALL UFILL_BLK", "(  IN := #i1 ,", "   COUNT := #u1 ,", "   OUT := #dst[0]", ");"]),
    34: ("SCATTER_BLK", ["CALL SCATTER_BLK", "{value_type := 'Byte'}", "(  IN := #bytes[0] ,", "   COUNT_IN := #u1 ,", "   OUT := #bits[0]", ");"]),
    58: ("SCALE_X", ["CALL SCALE_X", "{value_type := 'Real', dest_type := 'Int'}", "(  MIN := #i1 ,", "   VALUE := #r1 ,", "   MAX := #i2 ,", "   RET_VAL := #io", ");"]),
    59: ("NORM_X", ["CALL NORM_X", "{value_type := 'Int', dest_type := 'Real'}", "(  MIN := #i1 ,", "   VALUE := #i2 ,", "   MAX := #i1 ,", "   RET_VAL := #ro", ");"]),
    67: ("GET_ERROR", ["CALL GET_ERROR", "(  OUT := #err", ");"]),
    68: ("GET_ERR_ID", ["CALL GET_ERR_ID", "(  RET_VAL := #wo", ");"]),
    71: ("RUNTIME", ["CALL RUNTIME", "(  MEM := #rt ,", "   RET_VAL := #rt", ");"]),
    72: ("DECO", ["CALL DECO", "{value_type := 'DWord'}", "(  IN := #u1 ,", "   OUT := #dwo", ");"]),
    73: ("ENCO", ["CALL ENCO", "{value_type := 'DWord'}", "(  IN := #dw1 ,", "   OUT := #io", ");"]),
    82: ("BCDCPL", ["CALL BCDCPL", "(  IN := #dw1 ,", "   RET_VAL := #dwo", ");"]),
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        os.remove(os.path.join(OUT, f))
    for no, (name, call), itf in [(k, v, ITF) for k, v in CALLS.items()] + [(k, v, ITF2) for k, v in CALLS2.items()]:
        fb = "FB_StlC_%03d" % no
        body = ["      " + call[0]] + ["      " + c for c in call[1:]]
        text = "\n".join(['FUNCTION_BLOCK "%s"' % fb, "TITLE = STL call %s" % name, "{ S7_Optimized_Access := 'TRUE' }",
                          "VERSION : 0.1", itf, "BEGIN", "NETWORK", "TITLE = %03d %s" % (no, name)] + body + ["END_FUNCTION_BLOCK", ""])
        open(os.path.join(OUT, fb + ".awl"), "w", encoding="utf-8").write(text)
    print(len(CALLS) + len(CALLS2), "sources in", OUT)


if __name__ == "__main__":
    main()
