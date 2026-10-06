<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="2.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
  xmlns:a="http://www.altova.com/xslt-extensions" exclude-result-prefixes="a">
  <xsl:output method="xml" encoding="utf-8" indent="yes"/>
  <xsl:template match="/catalog">
    <xsl:variable name="unique" select="a:distinct-nodes(item)"/>
    <result input-count="{count(item)}" unique-count="{count($unique)}">
      <unique><xsl:copy-of select="$unique"/></unique>
      <empty count="{count(a:distinct-nodes(missing))}"/>
      <single count="{count(a:distinct-nodes(item[1]))}"/>
      <repeated-node count="{count(a:distinct-nodes((item[1], item[1])))}"/>
      <identity>
        <xsl:for-each select="$unique">
          <xsl:variable name="node" select="."/>
          <node source-positions="{string-join(for $i in 1 to count(/catalog/item) return if (/catalog/item[$i] is $node) then string($i) else (), ' ')}"/>
        </xsl:for-each>
      </identity>
    </result>
  </xsl:template>
</xsl:stylesheet>
