<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0"
  xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:a="http://www.altova.com/xslt-extensions"
  xmlns:local="urn:example:local"
  exclude-result-prefixes="a local">
  <xsl:function name="local:label">
    <xsl:param name="value"/>
    <xsl:sequence select="$value"/>
  </xsl:function>
  <xsl:template match="/">
    <result value="{a:distinct-nodes(//item)}">
      <xsl:value-of select="local:label('test')"/>
    </result>
  </xsl:template>
</xsl:stylesheet>
